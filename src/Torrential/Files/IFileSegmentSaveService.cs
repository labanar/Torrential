using System.Buffers;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IFileSegmentSaveService
    {
        void SaveSegment(PooledPieceSegment segment);
    }

    public class FileSegmentSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache)
        : IFileSegmentSaveService
    {
        public void SaveSegment(PooledPieceSegment segment)
        {
            using (segment)
            {
                if (!metaCache.TryGet(segment.InfoHash, out var meta))
                    return;

                var fileHandle = fileHandleProvider.GetFileHandle(segment.InfoHash);

                long fileOffset = (segment.Index * meta.PieceSize) + segment.Offset;
                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);
            }
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
