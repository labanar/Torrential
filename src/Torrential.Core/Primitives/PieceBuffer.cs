using System.Buffers;
using System.IO.Pipelines;
using Torrential.Core.Peers;
using Torrential.Core.Torrents;

namespace Torrential.Core.Primitives;


public class PieceBuffer : IAsyncDisposable, IDisposable
{
    private const int DEFAULT_SEGMENT_LENGTH = 16384;
    private bool _disposed;

    private readonly byte[] _buffer;
    public required InfoHash InfoHash { get; init; }
    public required byte[] PieceHash { get; init; }
    public required int PieceIndex { get; init; }
    public int PieceSize { get; }
    public void WriteSegment(int offset, ReadOnlySpan<byte> segment) => segment.CopyTo(_buffer.AsSpan(offset, segment.Length));
    public void WriteSegment(int offset, ReadOnlySequence<byte> segment) =>segment.CopyTo(_buffer.AsSpan(offset, (int)segment.Length));
    public Span<byte> GetSegmentSpan(int offset, int segmentLength) => _buffer.AsSpan(offset, segmentLength);

    public PieceBuffer(int pieceSize)
    {
        PieceSize = pieceSize;
        _buffer = ArrayPool<byte>.Shared.Rent(pieceSize);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
