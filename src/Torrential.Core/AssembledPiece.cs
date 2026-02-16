using System.Security.Cryptography;

namespace Torrential.Core;

public sealed class AssembledPiece : IDisposable
{
    private readonly PooledBlock[] _blocks;
    private readonly int _pieceSize;

    public int PieceIndex { get; }

    public AssembledPiece(int pieceIndex, PooledBlock[] blocks, int pieceSize)
    {
        PieceIndex = pieceIndex;
        _blocks = blocks;
        _pieceSize = pieceSize;
    }

    public bool Verify(ReadOnlySpan<byte> expectedHash)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        foreach (var block in _blocks)
            hasher.AppendData(block.Buffer);

        Span<byte> hash = stackalloc byte[20];
        hasher.GetHashAndReset(hash);
        return hash.SequenceEqual(expectedHash);
    }

    public void Dispose()
    {
        foreach (var block in _blocks)
            block.Dispose();
    }
}
