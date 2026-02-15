using System.Buffers;

namespace Torrential.Core;

public sealed class PooledBlock : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly int _size;
    private readonly byte[] _buffer;
    public ReadOnlySpan<byte> Buffer => _buffer.AsSpan().Slice(0, _size);

    public InfoHash InfoHash { get; private set; }
    public int PieceIndex { get; private set; }
    public int Offset { get; private set; }

    public static PooledBlock FromReadOnlySequence(ReadOnlySequence<byte> sequence, int chunkSize, InfoHash infoHash)
    {
        var reader = new SequenceReader<byte>(sequence);

        if (!reader.TryReadBigEndian(out int pieceIndex))
            throw new ArgumentException("Error reading block pieceIndex");
        if (!reader.TryReadBigEndian(out int pieceOffset))
            throw new ArgumentException("Error reading block offset");
        if (!reader.TryReadExact(chunkSize, out var blockSequence))
            throw new ArgumentException("Error reading piece block data");

        var block = new PooledBlock((int)blockSequence.Length, ArrayPool<byte>.Shared, infoHash, pieceIndex, pieceOffset);
        blockSequence.CopyTo(block._buffer);
        return block;
    }

    private PooledBlock(int size, ArrayPool<byte> pool, InfoHash infoHash, int index, int offset)
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
