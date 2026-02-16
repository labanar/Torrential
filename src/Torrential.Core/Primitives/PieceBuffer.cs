using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torrential.Core.Peers;
using Torrential.Core.Torrents;

namespace Torrential.Core.Primitives;


public class PieceBuffer : IAsyncDisposable, IDisposable
{
    private const int DEFAULT_SEGMENT_LENGTH = 16384;

    private readonly byte[] _buffer;
    public required InfoHash InfoHash { get; init; }
    public required byte[] PieceHash { get; init; }
    public required int PieceIndex { get; set; }
    public required int PieceSize { get; set; }
    public void WriteSegment(int offset, ReadOnlySpan<byte> segment) => segment.CopyTo(_buffer.AsSpan(offset, segment.Length));
    public void WriteSegment(int offset, ReadOnlySequence<byte> segment) =>segment.CopyTo(_buffer.AsSpan(offset, (int)segment.Length));
    public Span<byte> GetSegmentSpan(int offset, int segmentLength) => _buffer.AsSpan(offset, segmentLength);

    public PieceBuffer()
    {
        _buffer = ArrayPool<byte>.Shared.Rent(PieceSize);
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
