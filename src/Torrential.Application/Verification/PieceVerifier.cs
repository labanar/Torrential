using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.IO.Pipelines;
using Torrential.Application.Events;
using Torrential.Application.Files;
using Torrential.Application.Peers;
using Torrential.Application.Torrents;
using Torrential.Application.Utilities;

namespace Torrential.Application.Verification;

public class PieceVerifier(ILogger<PieceVerifier> logger, TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, BitfieldManager bitfieldMgr, IEventBus eventBus)
    : IPieceVerifier
{
    public async Task VerifyPieceAsync(InfoHash infoHash, int pieceIndex, CancellationToken cancellationToken = default)
    {
        if (!metaCache.TryGet(infoHash, out var meta))
        {
            logger.LogError("Could not find torrent metadata");
            return;
        }

        if (!bitfieldMgr.TryGetDownloadBitfield(infoHash, out var downloadBitfield))
            return;

        if (!bitfieldMgr.TryGetVerificationBitfield(infoHash, out var verificationBitfield))
            return;

        if (verificationBitfield.HasPiece(pieceIndex))
        {
            logger.LogInformation("Already verified piece {Piece} - ignoring", pieceIndex);
            return;
        }

        var fileHandle = await fileHandleProvider.GetPartFileHandle(infoHash);
        var buffer = ArrayPool<byte>.Shared.Rent(20);
        meta.GetPieceHash(pieceIndex).CopyTo(buffer);

        var pipe = PipePool.Shared.Get();
        var fillTask = FillPipeWithPiece(fileHandle, pipe.Writer, pieceIndex, (int)meta.PieceSize).ConfigureAwait(true);

        var pieceSize = pieceIndex == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize;
        var bufferSize = LargestPowerOf2ThatDividesX((int)pieceSize);
        var result = await Sha1Helper.VerifyHash(pipe.Reader, buffer, bufferSize);

        ArrayPool<byte>.Shared.Return(buffer);
        await fillTask;
        logger.LogDebug("Validation result for {Piece}: {Result}", pieceIndex, result);
        PipePool.Shared.Return(pipe);


        if (result)
        {
            await verificationBitfield.MarkHaveAsync(pieceIndex, CancellationToken.None);
            await eventBus.PublishAsync(new TorrentPieceVerifiedEvent { InfoHash = infoHash, PieceIndex = pieceIndex, Progress = verificationBitfield.CompletionRatio });
            if (verificationBitfield.HasAll())
            {
                logger.LogInformation("All pieces verified");
                await eventBus.PublishAsync(new TorrentCompleteEvent { InfoHash = infoHash });
            }
        }
        else
        {
            logger.LogWarning("Piece verification failed for {Piece}, unmarking from download bitfield", pieceIndex);
            await downloadBitfield.UnmarkHaveAsync(pieceIndex, CancellationToken.None);
        }
    }

    private static int LargestPowerOf2ThatDividesX(int x)
    {
        return x & -x;
    }

    private async Task FillPipeWithPiece(SafeFileHandle sfh, PipeWriter writer, int pieceIndex, int pieceSize)
    {
        var mem = writer.GetMemory(pieceSize);
        var offset = 1L * pieceIndex * pieceSize;
        var bytesRead = await RandomAccess.ReadAsync(sfh, mem, offset);
        writer.Advance(bytesRead);
        await writer.FlushAsync();
        await writer.CompleteAsync();
    }
}
