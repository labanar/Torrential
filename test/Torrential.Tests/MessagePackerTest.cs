using Torrential.Peers;

namespace Torrential.Tests;

public class MessagePackerTest
{
    [Fact]
    public void Pack_MessageId()
    {
        using var pak = MessagePacker.Pack(1);
        var span = pak.AsSpan();
        Assert.Equal(5, span.Length);
        Assert.Equal(1, span[4]);
    }

    [Fact]
    public void Pack_MessageId_P1()
    {
        using var pak = MessagePacker.Pack(1, 2);
        var span = pak.AsSpan();
        Assert.Equal(9, span.Length);
        Assert.Equal(1, span[4]);
        Assert.Equal(2, span.Slice(5).ReadBigEndianInt32());
    }

    [Fact]
    public void Pack_MessageId_P1_P2()
    {
        using var pak = MessagePacker.Pack(1, 2, 3);
        var span = pak.AsSpan();
        Assert.Equal(13, span.Length);
        Assert.Equal(1, span[4]);
        Assert.Equal(2, span.Slice(5).ReadBigEndianInt32());
        Assert.Equal(3, span.Slice(9).ReadBigEndianInt32());
    }

    [Fact]
    public void Pack_MessageId_P1_P2_P3()
    {
        using var pak = MessagePacker.Pack(1, 2, 3, 4);
        var span = pak.AsSpan();
        Assert.Equal(17, span.Length);
        Assert.Equal(1, span[4]);
        Assert.Equal(2, span.Slice(5).ReadBigEndianInt32());
        Assert.Equal(3, span.Slice(9).ReadBigEndianInt32());
        Assert.Equal(4, span.Slice(13).ReadBigEndianInt32());
    }

    [Fact]
    public void Pack_PieceData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var pak = MessagePacker.Pack(1, 2, data);
        var span = pak.AsSpan();
        Assert.Equal(13 + data.Length, span.Length);
        Assert.Equal(PeerWireMessageType.Piece, span[4]);
        Assert.Equal(1, span.Slice(5).ReadBigEndianInt32());
        Assert.Equal(2, span.Slice(9).ReadBigEndianInt32());
        Assert.Equal(data, span.Slice(13).ToArray());
    }

    [Fact]
    public void Pack_Bitfield()
    {
        var bitfield = new Bitfield(20);
        using var pak = MessagePacker.Pack(bitfield);
        var span = pak.AsSpan();
        Assert.Equal(5 + bitfield.Bytes.Length, span.Length);
        Assert.Equal(PeerWireMessageType.Bitfield, span[4]);
        Assert.Equal(bitfield.Bytes, span.Slice(5).ToArray());
    }
}
