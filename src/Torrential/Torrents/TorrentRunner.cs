﻿using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using Torrential.Files;
using Torrential.Peers;

namespace Torrential.Torrents
{
    public class TorrentRunner(ILogger<TorrentRunner> logger,
                               BitfieldManager bitfieldMgr,
                               IFileHandleProvider fileHandleProvider,
                               IFileSegmentSaveService segmentSaveService,
                               IBus bus,
                               PieceSelector pieceSelector)
    {

        public async Task StartSharing(TorrentMetadata meta, PeerWireClient peer, CancellationToken canellationToken)
        {
            var ctsWrap = CancellationTokenSource.CreateLinkedTokenSource(canellationToken);
            var stoppingToken = ctsWrap.Token;

            logger.LogInformation("Waiting for bitfield from peer");
            while (peer.State.PeerBitfield == null && !stoppingToken.IsCancellationRequested)
                await Task.Delay(100);

            if (peer.State.PeerBitfield == null)
            {
                logger.LogInformation("Peer did not send bitfield");
                return;
            }

            await bus.Publish(new PeerBitfieldReceivedEvent
            {
                HasAllPieces = peer.State.PeerBitfield.HasAll(),
                InfoHash = meta.InfoHash,
                PeerId = peer.PeerId
            });

            logger.LogInformation("Received bitfield from peer");

            var leechTask = LeechFromPeer(meta, peer, stoppingToken);
            var seedTask = SeedToPeer(meta, peer, stoppingToken);
            await Task.WhenAll(leechTask, seedTask);
            ctsWrap.Cancel();
        }


        private async Task LeechFromPeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken stoppingToken)
        {
            if (!bitfieldMgr.TryGetVerificationBitfield(meta.InfoHash, out var verificationBitfield))
            {
                logger.LogInformation("Failed to retrieve verification bitfield");
                return;
            }

            if (verificationBitfield.HasAll())
            {
                logger.LogInformation("Already have all pieces");
                await peer.SendNotInterested();
                return;
            }

            logger.LogInformation("Sending interested to peer");
            await peer.SendIntereted();


            logger.LogInformation("Waiting for unchoke from peer");
            while (peer.State.AmChoked && !stoppingToken.IsCancellationRequested)
                await Task.Delay(100);


            logger.LogInformation("Starting piece selection");
            var hasMorePieces = true;
            while (!peer.State.AmChoked && !stoppingToken.IsCancellationRequested && hasMorePieces)
            {
                //Start asking for pieces, wait for us to get a piece back then ask for the next piece
                var suggestion = await pieceSelector.SuggestNextPieceAsync(meta.InfoHash, peer.State.PeerBitfield);
                if (!suggestion.Index.HasValue)
                {
                    hasMorePieces = suggestion.MorePiecesAvailable;
                    await Task.Delay(50);
                    continue;
                }

                var requestSize = (int)Math.Pow(2, 14);
                var pieceSize = (int)(suggestion.Index == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize);
                var remainder = pieceSize;

                while (remainder > 0 && !stoppingToken.IsCancellationRequested)
                {
                    var offset = pieceSize - remainder;
                    var sizeToRequest = Math.Min(requestSize, remainder);
                    await peer.SendPieceRequest(suggestion.Index.Value, offset, sizeToRequest);
                    remainder -= sizeToRequest;
                }
            }

            logger.LogInformation("Finished requesting pieces from peer");
        }

        private async Task SeedToPeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken stoppingToken)
        {
            if (peer.State.PeerBitfield == null)
            {
                logger.LogInformation("Peer has not sent bitfield yet");
                return;
            }

            if (peer.State.PeerBitfield.HasAll())
            {
                logger.LogInformation("Peer has all pieces");
                return;
            }

            //Wait for peer to be interested then unchoke them
            while (!peer.State.PeerInterested && !stoppingToken.IsCancellationRequested)
                await Task.Delay(5000, stoppingToken);

            logger.LogInformation("Peer has shown interest, unchoking");
            await peer.SendUnchoke();

            //Wait for the peer to request a piece;
            await foreach (var request in peer.PeerPeieceRequests.Reader.ReadAllAsync(stoppingToken))
            {
                logger.LogInformation("Received piece {@Request} from peer", request);

                //TODO - add pieces to the superseed list as we go
                var buffer = ArrayPool<byte>.Shared.Rent(request.Length);
                try
                {
                    var fileHandle = await fileHandleProvider.GetPartFileHandle(meta.InfoHash);
                    long fileOffset = (request.PieceIndex * meta.PieceSize) + request.Begin;
                    RandomAccess.Read(fileHandle, buffer, fileOffset);
                    peer.SendPiece(request.PieceIndex, request.Begin, buffer, request.Length);

                    await bus.Publish(new TorrentSegmentUploadedEvent { InfoHash = meta.InfoHash, SegmentLength = request.Length });

                    logger.LogInformation("Sent piece {@Request} to peer", request);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}
