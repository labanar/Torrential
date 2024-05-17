using System.Buffers;

namespace Torrential.Peers;

public interface IPieceSegment
{
    public ReadOnlySpan<byte> Buffer { get; }
    public InfoHash InfoHash { get; }
    public int PieceIndex { get; }
    public int Offset { get; }
}

public sealed class PooledPieceSegment : IPieceSegment, IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly int _size;
    private readonly byte[] _buffer;
    public ReadOnlySpan<byte> Buffer => _buffer.AsSpan().Slice(0, _size);

    public InfoHash InfoHash { get; private set; }
    public int PieceIndex { get; private set; }
    public int Offset { get; private set; }

    public static PooledPieceSegment FromReadOnlySequence(ReadOnlySequence<byte> sequence, int chunkSize, InfoHash infoHash)
    {
        var reader = new SequenceReader<byte>(sequence);

        if (!reader.TryReadBigEndian(out int pieceIndex))
            throw new ArgumentException("Error reading piece index value");
        if (!reader.TryReadBigEndian(out int pieceOffset))
            throw new ArgumentException("Error reading piece offset value");
        if (!reader.TryReadExact(chunkSize, out var segmentSequence))
            throw new ArgumentException("Error reading piece segment value");

        var segment = new PooledPieceSegment((int)segmentSequence.Length, ArrayPool<byte>.Shared, infoHash, pieceIndex, pieceOffset);
        segmentSequence.CopyTo(segment._buffer);
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

