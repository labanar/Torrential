namespace Torrential.Core;

public sealed class PieceAssembler : IDisposable
{
    private readonly int _pieceIndex;
    private readonly int _pieceSize;
    private readonly int _blockSize;
    private readonly PooledBlock?[] _blocks;
    private int _receivedCount;
    private bool _completed;

    public int ExpectedBlockCount { get; }
    public bool IsComplete => _receivedCount == ExpectedBlockCount;

    public PieceAssembler(int pieceIndex, int pieceSize, int blockSize = 16384)
    {
        _pieceIndex = pieceIndex;
        _pieceSize = pieceSize;
        _blockSize = blockSize;
        ExpectedBlockCount = (pieceSize + blockSize - 1) / blockSize;
        _blocks = new PooledBlock?[ExpectedBlockCount];
    }

    public bool TryAddBlock(PooledBlock block)
    {
        if (block.PieceIndex != _pieceIndex)
            return false;

        var slot = block.Offset / _blockSize;
        if (slot < 0 || slot >= _blocks.Length)
            return false;

        if (_blocks[slot] is not null)
            return false;

        _blocks[slot] = block;
        _receivedCount++;
        return true;
    }

    public AssembledPiece Complete()
    {
        var blocks = new PooledBlock[ExpectedBlockCount];
        for (var i = 0; i < ExpectedBlockCount; i++)
            blocks[i] = _blocks[i]!;

        _completed = true;
        return new AssembledPiece(_pieceIndex, blocks, _pieceSize);
    }

    public void Dispose()
    {
        if (_completed)
            return;

        for (var i = 0; i < _blocks.Length; i++)
        {
            _blocks[i]?.Dispose();
            _blocks[i] = null;
        }
    }
}
