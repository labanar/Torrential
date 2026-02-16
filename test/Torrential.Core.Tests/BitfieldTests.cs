using System.Buffers;
using Torrential.Core.Peers;

namespace Torrential.Core.Tests;

public class BitfieldTests
{
    [Fact]
    public void NewBitfield_HasNone_ReturnsTrue()
    {
        using var bf = new Bitfield(16);
        Assert.True(bf.HasNone());
    }

    [Fact]
    public void NewBitfield_HasAll_ReturnsFalse()
    {
        using var bf = new Bitfield(16);
        Assert.False(bf.HasAll());
    }

    [Fact]
    public void NewBitfield_HasPiece_ReturnsFalse()
    {
        using var bf = new Bitfield(16);
        for (int i = 0; i < 16; i++)
            Assert.False(bf.HasPiece(i));
    }

    [Fact]
    public void MarkHave_SetsPiece()
    {
        using var bf = new Bitfield(16);
        bf.MarkHave(5);
        Assert.True(bf.HasPiece(5));
        Assert.False(bf.HasPiece(4));
        Assert.False(bf.HasPiece(6));
    }

    [Fact]
    public void MarkHave_MultiplePieces()
    {
        using var bf = new Bitfield(16);
        bf.MarkHave(0);
        bf.MarkHave(7);
        bf.MarkHave(8);
        bf.MarkHave(15);

        Assert.True(bf.HasPiece(0));
        Assert.True(bf.HasPiece(7));
        Assert.True(bf.HasPiece(8));
        Assert.True(bf.HasPiece(15));
        Assert.False(bf.HasPiece(1));
        Assert.False(bf.HasPiece(9));
    }

    [Fact]
    public void HasAll_WhenAllSet_ReturnsTrue()
    {
        using var bf = new Bitfield(8);
        for (int i = 0; i < 8; i++)
            bf.MarkHave(i);

        Assert.True(bf.HasAll());
    }

    [Fact]
    public void HasNone_AfterMarkHave_ReturnsFalse()
    {
        using var bf = new Bitfield(16);
        bf.MarkHave(3);
        Assert.False(bf.HasNone());
    }

    [Fact]
    public void CompletionRatio_Empty_IsZero()
    {
        using var bf = new Bitfield(16);
        Assert.Equal(0f, bf.CompletionRatio);
    }

    [Fact]
    public void CompletionRatio_Half_IsPointFive()
    {
        using var bf = new Bitfield(8);
        bf.MarkHave(0);
        bf.MarkHave(1);
        bf.MarkHave(2);
        bf.MarkHave(3);
        Assert.Equal(0.5f, bf.CompletionRatio);
    }

    [Fact]
    public void CompletionRatio_AllSet_IsOne()
    {
        using var bf = new Bitfield(8);
        for (int i = 0; i < 8; i++)
            bf.MarkHave(i);

        Assert.Equal(1f, bf.CompletionRatio);
    }

    [Fact]
    public void NumberOfPieces_MatchesConstructorArg()
    {
        using var bf = new Bitfield(42);
        Assert.Equal(42, bf.NumberOfPieces);
    }

    [Fact]
    public void Bytes_HasCorrectLength()
    {
        using var bf = new Bitfield(16);
        Assert.Equal(2, bf.Bytes.Length); // 16 pieces = 2 bytes

        using var bf2 = new Bitfield(9);
        Assert.Equal(2, bf2.Bytes.Length); // 9 pieces = 2 bytes (ceil(9/8))
    }

    [Fact]
    public void Fill_CopiesData()
    {
        using var bf = new Bitfield(8);
        Span<byte> data = stackalloc byte[] { 0b10101010 };
        bf.Fill(data);

        Assert.True(bf.HasPiece(0));
        Assert.False(bf.HasPiece(1));
        Assert.True(bf.HasPiece(2));
        Assert.False(bf.HasPiece(3));
    }

    [Fact]
    public void SpanConstructor_CopiesData()
    {
        Span<byte> data = stackalloc byte[] { 0xFF, 0x00 };
        using var bf = new Bitfield(data);

        Assert.Equal(16, bf.NumberOfPieces);
        for (int i = 0; i < 8; i++)
            Assert.True(bf.HasPiece(i));
        for (int i = 8; i < 16; i++)
            Assert.False(bf.HasPiece(i));
    }

    [Fact]
    public void ReadOnlySequenceConstructor_CopiesData()
    {
        var data = new byte[] { 0xFF, 0x00 };
        var sequence = new ReadOnlySequence<byte>(data);
        using var bf = new Bitfield(sequence);

        Assert.Equal(16, bf.NumberOfPieces);
        for (int i = 0; i < 8; i++)
            Assert.True(bf.HasPiece(i));
        for (int i = 8; i < 16; i++)
            Assert.False(bf.HasPiece(i));
    }

    [Fact]
    public void HasPiece_NegativeIndex_Throws()
    {
        using var bf = new Bitfield(8);
        Assert.Throws<ArgumentOutOfRangeException>(() => bf.HasPiece(-1));
    }

    [Fact]
    public void HasPiece_OutOfRange_Throws()
    {
        using var bf = new Bitfield(8);
        Assert.Throws<ArgumentOutOfRangeException>(() => bf.HasPiece(8));
    }

    [Fact]
    public void MarkHave_NegativeIndex_Throws()
    {
        using var bf = new Bitfield(8);
        Assert.Throws<ArgumentOutOfRangeException>(() => bf.MarkHave(-1));
    }

    [Fact]
    public void MarkHave_OutOfRange_Throws()
    {
        using var bf = new Bitfield(8);
        Assert.Throws<ArgumentOutOfRangeException>(() => bf.MarkHave(8));
    }

    [Fact]
    public void MarkHave_Idempotent()
    {
        using var bf = new Bitfield(8);
        bf.MarkHave(3);
        bf.MarkHave(3);
        Assert.True(bf.HasPiece(3));
    }
}
