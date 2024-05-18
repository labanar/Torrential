using MassTransit;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IBlockSaveService
    {
        Task SaveBlock(PooledBlock block);
    }

    internal class BlockSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache, ILogger<BlockSaveService> logger, BitfieldManager bitfieldManager, IBus bus)
        : IBlockSaveService
    {
        private static readonly int BLOCK_LENGTH = (int)Math.Pow(2, 14);
        private static ConcurrentDictionary<InfoHash, AsyncBitfield> _blockBitfields = [];

        public async Task SaveBlock(PooledBlock block)
        {
            using (block)
            {
                if (!metaCache.TryGet(block.InfoHash, out var meta))
                    return;

                //Have we already saved this piece?
                if (!bitfieldManager.TryGetVerificationBitfield(block.InfoHash, out var verificationBitfield))
                {
                    logger.LogError("Could not find download bitfield for {InfoHash}", block.InfoHash);
                    return;
                }

                //Have we already verified this piece?
                if (!bitfieldManager.TryGetDownloadBitfield(block.InfoHash, out var downloadBitfield))
                    return;

                if (verificationBitfield.HasPiece(block.PieceIndex))
                {
                    logger.LogDebug("Already downloaded {Piece} - ignoring block data!", block.PieceIndex);
                    return;
                }

                var blockBitfield = _blockBitfields.GetOrAdd(block.InfoHash, (_) =>
                {
                    return new AsyncBitfield(meta.TotalNumberOfChunks);
                });

                var fileHandle = await fileHandleProvider.GetPartFileHandle(block.InfoHash);
                var fileOffset = (block.PieceIndex * meta.PieceSize) + block.Offset;
                RandomAccess.Write(fileHandle, block.Buffer, fileOffset);
                await bus.Publish(new TorrentBlockDownloaded { InfoHash = meta.InfoHash, Length = block.Buffer.Length });

                var chunksPerFullPiece = (int)(meta.PieceSize / BLOCK_LENGTH);
                var chunkOffset = block.PieceIndex * chunksPerFullPiece;
                var pieceLength = (int)(block.PieceIndex == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize);
                var chunksInThisPiece = (int)Math.Ceiling((decimal)pieceLength / BLOCK_LENGTH);
                var extra = block.Offset / BLOCK_LENGTH;
                var chunkIndex = block.PieceIndex * chunksPerFullPiece + extra;
                await blockBitfield.MarkHaveAsync(chunkIndex, CancellationToken.None);

                if (HasAllBlocksForPiece(blockBitfield, block.PieceIndex, chunksInThisPiece, chunksPerFullPiece))
                {
                    await downloadBitfield.MarkHaveAsync(block.PieceIndex, CancellationToken.None);
                    await bus.Publish(new PieceValidationRequest { InfoHash = block.InfoHash, PieceIndex = block.PieceIndex });
                    await bus.Publish(new TorrentPieceDownloadedEvent { InfoHash = block.InfoHash, PieceIndex = block.PieceIndex });
                }
            }
        }


        public bool HasAllBlocksForPiece(AsyncBitfield chunkField, int pieceIndex, int chunksInThisPiece, int chunksInFullPiece)
        {
            var blockIndex = pieceIndex * chunksInFullPiece;
            for (int i = 0; i < chunksInThisPiece; i++)
            {
                if (!chunkField.HasPiece(blockIndex + i)) return false;
            }
            return true;
        }
    }
}
