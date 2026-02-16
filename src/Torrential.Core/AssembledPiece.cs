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

    public void WriteTo(Stream stream)
    {
        foreach (var block in _blocks)
            stream.Write(block.Buffer);
    }

    public void WriteRangeTo(Stream stream, int offset, int count)
    {
        var bytesSkipped = 0;
        var bytesWritten = 0;

        foreach (var block in _blocks)
        {
            var blockSize = block.Buffer.Length;

            if (bytesSkipped + blockSize <= offset)
            {
                bytesSkipped += blockSize;
                continue;
            }

            var startInBlock = Math.Max(0, offset - bytesSkipped);
            var available = blockSize - startInBlock;
            var toWrite = Math.Min(available, count - bytesWritten);

            stream.Write(block.Buffer.Slice(startInBlock, toWrite));
            bytesWritten += toWrite;
            bytesSkipped += blockSize;

            if (bytesWritten >= count)
                break;
        }
    }

    public void Dispose()
    {
        foreach (var block in _blocks)
            block.Dispose();
    }
}
