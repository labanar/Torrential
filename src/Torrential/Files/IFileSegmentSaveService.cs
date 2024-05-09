using MassTransit;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IFileSegmentSaveService
    {
        Task SaveSegment(PooledPieceSegment segment);
    }

    internal class FileSegmentSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache, ILogger<FileSegmentSaveService> logger, BitfieldManager bitfieldManager, IBus bus)
        : IFileSegmentSaveService
    {
        private static readonly int SEGMENT_LENGTH = (int)Math.Pow(2, 14);
        private static ConcurrentDictionary<InfoHash, AsyncBitfield> _segmentFields = [];

        public async Task SaveSegment(PooledPieceSegment segment)
        {
            using (segment)
            {
                if (!metaCache.TryGet(segment.InfoHash, out var meta))
                    return;

                //Have we already saved this piece?
                if (!bitfieldManager.TryGetDownloadBitfield(segment.InfoHash, out var downloadBitfield))
                {
                    logger.LogError("Could not find download bitfield for {InfoHash}", segment.InfoHash);
                    return;
                }

                if (downloadBitfield.HasPiece(segment.PieceIndex))
                {
                    logger.LogInformation("Already downloaded {Piece} - ignoring segment data!", segment.PieceIndex);
                    return;
                }

                var pieceSize = meta.PieceSize;
                var numSegmentsPerPiece = (int)(pieceSize / SEGMENT_LENGTH);
                var segmentField = _segmentFields.GetOrAdd(segment.InfoHash, (_) =>
                {
                    return new AsyncBitfield(meta.TotalNumberOfChunks);
                });

                var fileHandle = await fileHandleProvider.GetPartFileHandle(segment.InfoHash);
                var fileOffset = (segment.PieceIndex * meta.PieceSize) + segment.Offset;
                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);
                await bus.Publish(new TorrentSegmentDownloadedEvent { InfoHash = meta.InfoHash, SegmentLength = segment.Buffer.Length });

                var chunksPerFullPiece = (int)(meta.PieceSize / SEGMENT_LENGTH);
                var chunkOffset = segment.PieceIndex * chunksPerFullPiece;
                var pieceLength = (int)(segment.PieceIndex == meta.NumberOfPieces - 1 ? meta.FinalPieceSize : meta.PieceSize);
                var chunksInThisPiece = (int)Math.Ceiling((decimal)pieceLength / SEGMENT_LENGTH);
                var extra = segment.Offset / SEGMENT_LENGTH;
                var chunkIndex = segment.PieceIndex * chunksPerFullPiece + extra;
                await segmentField.MarkHaveAsync(chunkIndex, CancellationToken.None);

                if (HasAllSegmentsForPiece(segmentField, segment.PieceIndex, chunksInThisPiece, chunksPerFullPiece))
                {
                    await downloadBitfield.MarkHaveAsync(segment.PieceIndex, CancellationToken.None);
                    await bus.Publish(new PieceValidationRequest { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                    await bus.Publish(new TorrentPieceDownloadedEvent { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                }
            }
        }


        public bool HasAllSegmentsForPiece(AsyncBitfield chunkField, int pieceIndex, int chunksInThisPiece, int chunksInFullPiece)
        {
            var segmentIndex = pieceIndex * chunksInFullPiece;
            for (int i = 0; i < chunksInThisPiece; i++)
            {
                if (!chunkField.HasPiece(segmentIndex + i)) return false;
            }
            return true;
        }
    }
}
