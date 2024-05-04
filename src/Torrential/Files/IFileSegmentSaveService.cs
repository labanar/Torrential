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
        private static ConcurrentDictionary<InfoHash, Bitfield> _segmentFields = [];

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
                    return new Bitfield(numSegmentsPerPiece * meta.NumberOfPieces);
                });


                var fileHandle = await fileHandleProvider.GetPartFileHandle(segment.InfoHash);

                long fileOffset = (segment.PieceIndex * meta.PieceSize) + segment.Offset;
                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);

                var remainder = pieceSize % SEGMENT_LENGTH;
                if (remainder > 0)
                    numSegmentsPerPiece += 1;

                var segmentFieldIndex = segment.PieceIndex * numSegmentsPerPiece;
                var offsetIdx = segment.Offset / SEGMENT_LENGTH;
                segmentFieldIndex += offsetIdx;
                await segmentField.MarkHaveAsync(segmentFieldIndex, CancellationToken.None);
                if (HasAllSegmentsForPiece(segment.InfoHash, segment.PieceIndex, numSegmentsPerPiece))
                {
                    await downloadBitfield.MarkHaveAsync(segment.PieceIndex, CancellationToken.None);
                    await bus.Publish(new PieceValidationRequest { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                    await bus.Publish(new TorrentPieceDownloadedEvent { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                }
            }
        }

        public bool HasAllSegmentsForPiece(InfoHash infoHah, int pieceIndex, int segmentsPerPiece)
        {
            if (!_segmentFields.TryGetValue(infoHah, out var segmentField))
                return false;

            var segmentIndex = pieceIndex * segmentsPerPiece;
            for (int i = 0; i < segmentsPerPiece; i++)
            {
                if (!segmentField.HasPiece(segmentIndex + i)) return false;
            }

            return true;
        }
    }
}
