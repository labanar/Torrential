using System.Buffers;

namespace Torrential.Peers;

public sealed class PooledPieceSegment : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly int _size;
    private readonly byte[] _buffer;
    public ReadOnlySpan<byte> Buffer => _buffer.AsSpan().Slice(0, _size);

    public InfoHash InfoHash { get; private set; }
    public int PieceIndex { get; private set; }
    public int Offset { get; private set; }

    public static PooledPieceSegment FromReadOnlySequence(ref ReadOnlySequence<byte> sequence, InfoHash infoHash, int index, int offset)
    {
        var segment = new PooledPieceSegment((int)sequence.Length, ArrayPool<byte>.Shared, infoHash, index, offset);
        segment.Fill(ref sequence);
        return segment;
    }

    private PooledPieceSegment(int size, ArrayPool<byte> pool, InfoHash infoHash, int index, int offset)
    {
        InfoHash = infoHash;
        PieceIndex = index;
        Offset = offset;
        _pool = pool;
        _size = size;
        _buffer = _pool.Rent(size);
    }

    public void Fill(ref ReadOnlySequence<byte> sequence)
    {
        sequence.CopyTo(_buffer);
    }

    public void Dispose()
    {
        _pool.Return(_buffer);
    }
}

internal interface IPooledPacket : IDisposable
{
    Task WriteTo(Span<byte> destination);
}

