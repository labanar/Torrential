using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IFileSegmentSaveService
    {
        void SaveSegment(PooledPieceSegment segment);
    }

    public class FileSegmentSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache, ILogger<FileSegmentSaveService> logger)
        : IFileSegmentSaveService
    {

        private static readonly int SEGMENT_LENGTH = (int)Math.Pow(2, 14);
        private ConcurrentDictionary<InfoHash, Bitfield> _segmentFields = [];

        public void SaveSegment(PooledPieceSegment segment)
        {
            using (segment)
            {
                if (!metaCache.TryGet(segment.InfoHash, out var meta))
                    return;

                var fileHandle = fileHandleProvider.GetFileHandle(segment.InfoHash);

                long fileOffset = (segment.PieceIndex * meta.PieceSize) + segment.Offset;
                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);

                var pieceSize = meta.PieceSize;
                var numSegmentsPerPiece = (int)(pieceSize / SEGMENT_LENGTH);
                var remainder = pieceSize % SEGMENT_LENGTH;
                if (remainder > 0)
                    numSegmentsPerPiece += 1;


                var segmentField = _segmentFields.GetOrAdd(segment.InfoHash, (_) =>
                {
                    return new Bitfield(numSegmentsPerPiece * meta.NumberOfPieces);
                });


                var segmentFieldIndex = segment.PieceIndex * numSegmentsPerPiece;
                var offsetIdx = segment.Offset / SEGMENT_LENGTH;
                segmentFieldIndex += offsetIdx;
                segmentField.MarkHave(segmentFieldIndex);

                if (HasAllSegmentsForPiece(segment.InfoHash, segment.PieceIndex, numSegmentsPerPiece))
                {
                    logger.LogInformation("FULLY DOWNLOADED {PIECE}", segment.PieceIndex);
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
                if (!segmentField.HasPiece(segmentIndex)) return false;
            }

            return true;
        }
    }

    public sealed class PieceSegment
        : IAsyncDisposable, IDisposable
    {
        private readonly InfoHash _hash;
        private readonly byte[] _buffer;
        private readonly int _index;
        private readonly int _offset;
        private readonly int _length;
        public InfoHash InfoHash => _hash;
        public int Index => _index;
        public int Offset => _offset;
        public ReadOnlySpan<byte> Payload => _buffer.AsSpan()[.._length];

        public PieceSegment(InfoHash infoHash, int index, int offset, ReadOnlySpan<byte> payload)
        {
            _hash = infoHash;
            _buffer = ArrayPool<byte>.Shared.Rent(payload.Length);
            _index = index;
            _offset = offset;
            _length = payload.Length;
            payload.CopyTo(_buffer);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        public ValueTask DisposeAsync()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            return ValueTask.CompletedTask;
        }
    }
}
