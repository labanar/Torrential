using System.Buffers;
using System.Security.Cryptography;

namespace Torrential.Peers;

public sealed class PieceBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _pieceLength;
    private readonly int _blockSize = 16384;
    private readonly int _totalBlocks;
    private readonly bool[] _receivedBlocks;
    private int _blocksReceived;

    public PeerId Owner { get; private set; }
    public int PieceIndex { get; }

    public PieceBuffer(int pieceIndex, int pieceLength, PeerId owner)
    {
        PieceIndex = pieceIndex;
        _pieceLength = pieceLength;
        Owner = owner;
        _buffer = ArrayPool<byte>.Shared.Rent(pieceLength);
        _totalBlocks = (pieceLength + _blockSize - 1) / _blockSize;
        _receivedBlocks = new bool[_totalBlocks];
    }

    public bool TryAddBlock(ReadOnlySpan<byte> blockData, int offset, PeerId sender)
    {
        if (sender != Owner)
            return false;

        var blockIndex = offset / _blockSize;
        if (blockIndex < 0 || blockIndex >= _totalBlocks)
            return false;

        if (_receivedBlocks[blockIndex])
            return false;

        blockData.CopyTo(_buffer.AsSpan(offset, blockData.Length));
        _receivedBlocks[blockIndex] = true;
        _blocksReceived++;
        return true;
    }

    public bool IsComplete => _blocksReceived == _totalBlocks;

    public bool VerifyHash(ReadOnlySpan<byte> expectedHash)
    {
        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(_buffer.AsSpan(0, _pieceLength), hash);
        return hash.SequenceEqual(expectedHash);
    }

    public void Reassign(PeerId newOwner)
    {
        Owner = newOwner;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
