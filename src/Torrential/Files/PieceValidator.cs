using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using Torrential.Peers;
using Torrential.Torrents;
using Torrential.Utilities;

namespace Torrential.Files
{
    public class PieceValidationRequest
    {
        public required InfoHash InfoHash { get; init; }
        public required int PieceIndex { get; init; }
    }

    public class PieceValidator(ILogger<PieceValidator> logger, TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, BitfieldManager bitfieldMgr, IBus bus)
        : IConsumer<PieceValidationRequest>
    {
        // Tracks whether TorrentCompleteEvent has already been published for each torrent.
        // TryAdd is atomic: only the first caller to add the key succeeds, guaranteeing exactly-once publish.
        private static readonly ConcurrentDictionary<InfoHash, byte> _completionPublished = new();

        public async Task Consume(ConsumeContext<PieceValidationRequest> context)
        {
            var request = context.Message;

            if (!metaCache.TryGet(request.InfoHash, out var meta))
            {
                logger.LogError("Could not find torrent metadata");
                return;
            }

            if (!bitfieldMgr.TryGetDownloadBitfield(request.InfoHash, out var downloadBitfield))
                return;

            if (!bitfieldMgr.TryGetVerificationBitfield(request.InfoHash, out var verificationBitfield))
                return;

            if (verificationBitfield.HasPiece(request.PieceIndex))
            {
                logger.LogInformation("Already verified piece {Piece} - ignoring", request.PieceIndex);
                return;
            }

            var fileHandle = await fileHandleProvider.GetPartFileHandle(request.InfoHash);
            var buffer = ArrayPool<byte>.Shared.Rent(20);
            meta.GetPieceHash(request.PieceIndex).CopyTo(buffer);

            var pipe = PipePool.Shared.Get();
            var fillTask = FillPipeWithPiece(fileHandle, pipe.Writer, request.PieceIndex, (int)meta.PieceSize).ConfigureAwait(true);

            var pieceSize = request.PieceIndex == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize;
            var bufferSize = LargestPowerOf2ThatDividesX((int)pieceSize);
            var result = await Sha1Helper.VerifyHash(pipe.Reader, buffer, bufferSize);

            ArrayPool<byte>.Shared.Return(buffer);
            await fillTask;
            logger.LogDebug("Validation result for {Piece}: {Result}", request.PieceIndex, result);
            PipePool.Shared.Return(pipe);


            if (result)
            {
                verificationBitfield.MarkHave(request.PieceIndex);
                await bus.Publish(new TorrentPieceVerifiedEvent { InfoHash = request.InfoHash, PieceIndex = request.PieceIndex, Progress = verificationBitfield.CompletionRatio });

                if (verificationBitfield.HasAll())
                {
                    // Atomic guard: TryAdd returns true only for the first thread that inserts the key.
                    // All concurrent threads that also see HasAll() == true will get false from TryAdd
                    // and skip the publish, guaranteeing exactly one TorrentCompleteEvent per torrent.
                    if (_completionPublished.TryAdd(request.InfoHash, 1))
                    {
                        logger.LogInformation("All pieces verified");
                        await bus.Publish(new TorrentCompleteEvent { InfoHash = request.InfoHash });
                    }
                    else
                    {
                        logger.LogDebug("All pieces verified but completion event already published — skipping duplicate");
                    }
                }
            }
            else
            {
                logger.LogWarning("Piece verification failed for {Piece}, unmarking from download bitfield", request.PieceIndex);
                downloadBitfield.UnmarkHave(request.PieceIndex);
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
}
