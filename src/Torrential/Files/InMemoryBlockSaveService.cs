using MassTransit;
using Microsoft.Extensions.Logging;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files;

internal class InMemoryBlockSaveService(
    TorrentMetadataCache metaCache,
    ILogger<InMemoryBlockSaveService> logger,
    BitfieldManager bitfieldManager,
    IBus bus,
    PieceBufferManager pieceBufferManager,
    PieceReservationService pieceReservationService)
    : IBlockSaveService
{
    public async Task SaveBlock(PooledBlock block)
    {
        using (block)
        {
            if (!metaCache.TryGet(block.InfoHash, out var meta))
                return;

            if (!bitfieldManager.TryGetVerificationBitfield(block.InfoHash, out var verificationBitfield))
                return;

            if (verificationBitfield.HasPiece(block.PieceIndex))
                return;

            var pieceLength = (int)(block.PieceIndex == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize);

            var buffer = pieceBufferManager.GetOrCreateBuffer(block.InfoHash, block.PieceIndex, pieceLength, block.SenderPeerId);

            if (!buffer.TryAddBlock(block.Buffer, block.Offset, block.SenderPeerId))
                return;

            await bus.Publish(new TorrentBlockDownloaded { InfoHash = meta.InfoHash, Length = block.Buffer.Length });

            if (buffer.IsComplete)
            {
                var expectedHash = meta.GetPieceHash(block.PieceIndex);
                var valid = buffer.VerifyHash(expectedHash);

                if (valid)
                {
                    await verificationBitfield.MarkHaveAsync(block.PieceIndex, CancellationToken.None);
                    await bus.Publish(new TorrentPieceVerifiedEvent { InfoHash = block.InfoHash, PieceIndex = block.PieceIndex, Progress = verificationBitfield.CompletionRatio });
                    logger.LogInformation("Piece {PieceIndex} verified successfully", block.PieceIndex);

                    if (verificationBitfield.HasAll())
                        await bus.Publish(new TorrentCompleteEvent { InfoHash = block.InfoHash });
                }
                else
                {
                    logger.LogWarning("Piece {PieceIndex} failed verification, releasing reservation", block.PieceIndex);
                    pieceReservationService.ReleasePiece(block.InfoHash, block.PieceIndex);
                }

                pieceBufferManager.RemoveBuffer(block.InfoHash, block.PieceIndex);
            }
        }
    }
}
