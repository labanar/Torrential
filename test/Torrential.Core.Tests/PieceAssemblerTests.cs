using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Torrential.Core;
using Xunit;

namespace Torrential.Core.Tests;

public class PieceAssemblerTests
{
    private static readonly InfoHash TestInfoHash =
        InfoHash.FromHexString("0102030405060708091011121314151617181920");

    private static PooledBlock CreateBlock(int pieceIndex, int offset, byte[] data)
    {
        // PooledBlock.FromReadOnlySequence expects: 4-byte pieceIndex (BE) + 4-byte offset (BE) + data
        var payload = new byte[8 + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), pieceIndex);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4, 4), offset);
        data.CopyTo(payload, 8);

        var sequence = new ReadOnlySequence<byte>(payload);
        return PooledBlock.FromReadOnlySequence(sequence, data.Length, TestInfoHash);
    }

    private static PooledBlock CreateBlock(int pieceIndex, int offset, int size, byte fill = 0x00)
    {
        var data = new byte[size];
        Array.Fill(data, fill);
        return CreateBlock(pieceIndex, offset, data);
    }

    [Fact]
    public void TryAddBlock_AcceptsBlocksInOrder()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        Assert.True(assembler.TryAddBlock(CreateBlock(0, 0, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 16384, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 32768, 16384)));

        Assert.True(assembler.IsComplete);
        assembler.Complete().Dispose();
    }

    [Fact]
    public void TryAddBlock_AcceptsBlocksOutOfOrder()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        Assert.True(assembler.TryAddBlock(CreateBlock(0, 32768, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 0, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 16384, 16384)));

        Assert.True(assembler.IsComplete);
        assembler.Complete().Dispose();
    }

    [Fact]
    public void TryAddBlock_RejectsDuplicateOffset()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        Assert.True(assembler.TryAddBlock(CreateBlock(0, 0, 16384)));
        Assert.False(assembler.TryAddBlock(CreateBlock(0, 0, 16384)));
        Assert.False(assembler.IsComplete);

        assembler.Dispose();
    }

    [Fact]
    public void TryAddBlock_RejectsWrongPieceIndex()
    {
        var assembler = new PieceAssembler(pieceIndex: 5, pieceSize: 49152);

        Assert.False(assembler.TryAddBlock(CreateBlock(3, 0, 16384)));
        Assert.False(assembler.IsComplete);

        assembler.Dispose();
    }

    [Fact]
    public void Verify_CorrectHash_ReturnsTrue()
    {
        const int pieceSize = 49152;
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: pieceSize);

        // All zeros — build the full piece data to compute the expected hash
        var fullPiece = new byte[pieceSize];
        var expectedHash = SHA1.HashData(fullPiece);

        assembler.TryAddBlock(CreateBlock(0, 0, 16384));
        assembler.TryAddBlock(CreateBlock(0, 16384, 16384));
        assembler.TryAddBlock(CreateBlock(0, 32768, 16384));

        using var piece = assembler.Complete();
        Assert.True(piece.Verify(expectedHash));
    }

    [Fact]
    public void Verify_IncorrectHash_ReturnsFalse()
    {
        const int pieceSize = 49152;
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: pieceSize);

        assembler.TryAddBlock(CreateBlock(0, 0, 16384));
        assembler.TryAddBlock(CreateBlock(0, 16384, 16384));
        assembler.TryAddBlock(CreateBlock(0, 32768, 16384));

        var wrongHash = new byte[20];
        Array.Fill(wrongHash, (byte)0xFF);

        using var piece = assembler.Complete();
        Assert.False(piece.Verify(wrongHash));
    }

    [Fact]
    public void Dispose_DisposesPartialBlocks()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        assembler.TryAddBlock(CreateBlock(0, 0, 16384));
        assembler.TryAddBlock(CreateBlock(0, 16384, 16384));

        // Dispose with partial blocks — should not throw
        assembler.Dispose();
    }

    [Fact]
    public void Dispose_AfterComplete_DoesNotDoubleDispose()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        assembler.TryAddBlock(CreateBlock(0, 0, 16384));
        assembler.TryAddBlock(CreateBlock(0, 16384, 16384));
        assembler.TryAddBlock(CreateBlock(0, 32768, 16384));

        var piece = assembler.Complete();

        // Assembler should skip disposal since ownership was transferred
        assembler.Dispose();

        // AssembledPiece owns the blocks now
        piece.Dispose();
    }

    [Fact]
    public void AssembledPiece_Dispose_DisposesAllBlocks()
    {
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: 49152);

        assembler.TryAddBlock(CreateBlock(0, 0, 16384));
        assembler.TryAddBlock(CreateBlock(0, 16384, 16384));
        assembler.TryAddBlock(CreateBlock(0, 32768, 16384));

        var piece = assembler.Complete();
        piece.Dispose();
    }

    [Fact]
    public void LastPiece_SmallerSize()
    {
        // pieceSize = 40000 → 16384 + 16384 + 7232 = 3 blocks
        const int pieceSize = 40000;
        var assembler = new PieceAssembler(pieceIndex: 0, pieceSize: pieceSize);

        Assert.Equal(3, assembler.ExpectedBlockCount);

        Assert.True(assembler.TryAddBlock(CreateBlock(0, 0, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 16384, 16384)));
        Assert.True(assembler.TryAddBlock(CreateBlock(0, 32768, 7232)));

        Assert.True(assembler.IsComplete);
        assembler.Complete().Dispose();
    }
}
