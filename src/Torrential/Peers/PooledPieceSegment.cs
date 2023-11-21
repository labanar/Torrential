using System.Buffers;

namespace Torrential.Peers;

internal sealed class PooledPieceSegment : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly byte[] _buffer;

    public byte[] Buffer => _buffer;

    public static PooledPieceSegment FromReadOnlySequence(ref ReadOnlySequence<byte> sequence)
    {
        var segment = new PooledPieceSegment((int)sequence.Length, ArrayPool<byte>.Shared);
        segment.Fill(ref sequence);
        return segment;
    }

    private PooledPieceSegment(int size, ArrayPool<byte> pool)
    {
        _pool = pool;
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

