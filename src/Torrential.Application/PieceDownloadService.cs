using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public sealed class PieceDownloadService(
    ITorrentManager torrentManager,
    PeerConnectionService peerConnectionService,
    IPieceStorage pieceStorage,
    ILogger<PieceDownloadService> logger) : BackgroundService
{
    private const int BlockSize = 16384;
    private readonly ConcurrentDictionary<InfoHash, Bitfield> _localBitfields = new();
    private readonly ConcurrentDictionary<(InfoHash, int), byte> _inFlightPieces = new();
    private readonly ConcurrentDictionary<InfoHash, Task> _initTasks = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PieceDownloadService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ProcessDownloads(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in PieceDownloadService loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        logger.LogInformation("PieceDownloadService stopped");
    }

    private void ProcessDownloads(CancellationToken stoppingToken)
    {
        var activeTorrents = torrentManager.GetTorrentsByStatus(TorrentStatus.Downloading);

        foreach (var infoHash in activeTorrents)
        {
            var metaInfo = torrentManager.GetMetaInfo(infoHash);
            if (metaInfo is null) continue;

            var torrentState = torrentManager.GetState(infoHash);
            if (torrentState is null) continue;

            var initTask = _initTasks.GetOrAdd(infoHash, _ => pieceStorage.InitializeTorrentStorageAsync(metaInfo));
            if (!initTask.IsCompleted)
                continue;
            if (initTask.IsFaulted)
            {
                logger.LogError(initTask.Exception, "Failed to initialize storage for torrent {InfoHash}", infoHash.AsString());
                continue;
            }

            var localBitfield = _localBitfields.GetOrAdd(infoHash, _ => new Bitfield(torrentState.NumberOfPieces));
            var connectedPeers = peerConnectionService.GetConnectedPeers(infoHash);

            foreach (var peer in connectedPeers)
            {
                if (peer.Bitfield is null) continue;

                var suggestion = localBitfield.SuggestPieceToDownload(peer.Bitfield);
                if (suggestion.Index is null) continue;

                var pieceIndex = suggestion.Index.Value;

                // Skip if this piece is already being downloaded
                if (!_inFlightPieces.TryAdd((infoHash, pieceIndex), 0))
                    continue;

                var client = peerConnectionService.GetPeerClient(infoHash, peer.PeerInfo);
                if (client is null)
                {
                    _inFlightPieces.TryRemove((infoHash, pieceIndex), out _);
                    continue;
                }

                if (client.State.AmChoked)
                {
                    _inFlightPieces.TryRemove((infoHash, pieceIndex), out _);
                    _ = SendInterestedAsync(client);
                    continue;
                }

                var capturedPieceIndex = pieceIndex;
                var capturedMetaInfo = metaInfo;
                var capturedLocalBitfield = localBitfield;
                var capturedInfoHash = infoHash;

                _ = Task.Run(async () =>
                {
                    await DownloadPieceAsync(capturedInfoHash, capturedPieceIndex, capturedMetaInfo, capturedLocalBitfield, client, stoppingToken);
                }, stoppingToken);
            }
        }
    }

    private static async Task SendInterestedAsync(PeerWireClient client)
    {
        try
        {
            await client.SendInterested();
        }
        catch
        {
            // Best effort — peer may be disconnected
        }
    }

    private async Task DownloadPieceAsync(
        InfoHash infoHash,
        int pieceIndex,
        TorrentMetaInfo metaInfo,
        Bitfield localBitfield,
        PeerWireClient client,
        CancellationToken stoppingToken)
    {
        var pieceSize = (int)(pieceIndex == metaInfo.NumberOfPieces - 1
            ? metaInfo.TotalSize - (long)pieceIndex * metaInfo.PieceSize
            : metaInfo.PieceSize);

        var assembler = new PieceAssembler(pieceIndex, pieceSize, BlockSize);
        try
        {
            // Send request messages for all blocks in this piece
            var numberOfBlocks = (pieceSize + BlockSize - 1) / BlockSize;
            for (var i = 0; i < numberOfBlocks; i++)
            {
                var offset = i * BlockSize;
                var blockLength = Math.Min(BlockSize, pieceSize - offset);
                await client.SendPieceRequest(pieceIndex, offset, blockLength);
            }

            // Read blocks from the channel
            while (!assembler.IsComplete)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                if (!await client.InboundBlocks.WaitToReadAsync(stoppingToken))
                    break; // Channel completed — peer disconnected

                while (client.InboundBlocks.TryRead(out var block))
                {
                    if (block.PieceIndex != pieceIndex)
                    {
                        block.Dispose();
                        continue;
                    }

                    if (!assembler.TryAddBlock(block))
                    {
                        block.Dispose();
                        continue;
                    }

                    if (assembler.IsComplete)
                        break;
                }
            }

            if (!assembler.IsComplete)
            {
                logger.LogWarning("Piece {PieceIndex} download incomplete for torrent {InfoHash} — peer disconnected",
                    pieceIndex, infoHash.AsString());
                return;
            }

            using var assembled = assembler.Complete();

            var expectedHash = metaInfo.PieceHashes.AsSpan(pieceIndex * 20, 20);
            if (assembled.Verify(expectedHash))
            {
                await pieceStorage.WritePieceAsync(infoHash, pieceIndex, metaInfo, assembled);
                localBitfield.MarkHave(pieceIndex);
                logger.LogInformation("Piece {PieceIndex} verified and written to disk for torrent {InfoHash}",
                    pieceIndex, infoHash.AsString());

                // Check if any files are now complete
                for (var i = 0; i < metaInfo.Files.Count; i++)
                {
                    if (pieceStorage.IsFileComplete(infoHash, i, metaInfo, localBitfield))
                    {
                        await pieceStorage.FinalizeFileAsync(infoHash, i, metaInfo);
                    }
                }
            }
            else
            {
                logger.LogWarning("Piece {PieceIndex} failed hash verification for torrent {InfoHash}",
                    pieceIndex, infoHash.AsString());
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading piece {PieceIndex} for torrent {InfoHash}",
                pieceIndex, infoHash.AsString());
        }
        finally
        {
            assembler.Dispose();
            _inFlightPieces.TryRemove((infoHash, pieceIndex), out _);
        }
    }
}
