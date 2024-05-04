﻿using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
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
        public async Task Consume(ConsumeContext<PieceValidationRequest> context)
        {
            var request = context.Message;

            if (!metaCache.TryGet(request.InfoHash, out var meta))
            {
                logger.LogError("Could not find torrent metadata");
                return;
            }

            //Have we already verified this piece?
            if (!bitfieldMgr.TryGetDownloadBitfield(request.InfoHash, out var downloadBitfield))
                return;

            if (!bitfieldMgr.TryGetVerificationBitfield(request.InfoHash, out var verificationBitfield))
                return;

            if (verificationBitfield.HasPiece(request.PieceIndex))
            {
                logger.LogInformation("Already verified piece {Piece} - ignoring", request.PieceIndex);
                return;
            }

            //Let's save the piece and mark it as downloaded


            var fileHandle = await fileHandleProvider.GetPartFileHandle(request.InfoHash);
            var buffer = ArrayPool<byte>.Shared.Rent(20);
            meta.GetPieceHash(request.PieceIndex).CopyTo(buffer);

            var pipe = new System.IO.Pipelines.Pipe();
            var fillTask = FillPipeWithPiece(fileHandle, pipe.Writer, request.PieceIndex, (int)meta.PieceSize).ConfigureAwait(true);
            var result = await Sha1Helper.VerifyHash(pipe.Reader, buffer, Sha1Helper.CHUNK_SIZE);
            ArrayPool<byte>.Shared.Return(buffer);
            await fillTask;
            logger.LogInformation("Validation result for {Piece}: {Result}", request.PieceIndex, result);

            if (result)
            {
                verificationBitfield.MarkHave(request.PieceIndex);
                await bus.Publish(new TorrentPieceVerifiedEvent { InfoHash = request.InfoHash, PieceIndex = request.PieceIndex });
                if (verificationBitfield.HasAll())
                {
                    logger.LogInformation("All pieces verified");
                    await bus.Publish(new TorrentCompleteEvent { InfoHash = request.InfoHash });
                }
            }
            else
            {
                logger.LogWarning("Piece verification failed for {Piece}, unmarking from download bitfield", request.PieceIndex);
                await downloadBitfield.UnmarkHaveAsync(request.PieceIndex, CancellationToken.None);
            }
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
