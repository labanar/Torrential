using System.Buffers.Binary;
using Torrential.Peers;

namespace Torrential.Tests;

public class PeerPacketTest
{
    [Fact]
    public void ChokeMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new ChokeMessage());
        var span = pak.AsSpan();
        Assert.Equal(5, span.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Choke, span[4]);
    }

    [Fact]
    public void UnchokeMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new UnchokeMessage());
        var span = pak.AsSpan();
        Assert.Equal(5, span.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Unchoke, span[4]);
    }

    [Fact]
    public void InterestedMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new InterestedMessage());
        var span = pak.AsSpan();
        Assert.Equal(5, span.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Interested, span[4]);
    }

    [Fact]
    public void NotInterestedMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new NotInterestedMessage());
        var span = pak.AsSpan();
        Assert.Equal(5, span.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.NotInterested, span[4]);
    }

    [Fact]
    public void HaveMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new HaveMessage(42));
        var span = pak.AsSpan();
        Assert.Equal(9, span.Length);
        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Have, span[4]);
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(span[5..]));
    }

    [Fact]
    public void PieceRequestMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new PieceRequestMessage(1, 2, 3));
        var span = pak.AsSpan();
        Assert.Equal(17, span.Length);
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Request, span[4]);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span[5..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(span[9..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(span[13..]));
    }

    [Fact]
    public void CancelMessage_WritesCorrectPacket()
    {
        using var pak = PreparedPacket.FromPeerPacket(new CancelMessage(1, 2, 3));
        var span = pak.AsSpan();
        Assert.Equal(17, span.Length);
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Cancel, span[4]);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span[5..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(span[9..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(span[13..]));
    }

    [Fact]
    public void PreparedPieceMessage_WritesCorrectPacket()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var pak = PreparedPieceMessage.Create(1, 2, data);
        var span = pak.AsSpan();
        Assert.Equal(13 + data.Length, span.Length);
        Assert.Equal(9 + data.Length, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Piece, span[4]);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(span[5..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(span[9..]));
        Assert.Equal(data, span.Slice(13).ToArray());
    }

    [Fact]
    public void BitfieldMessage_WritesCorrectPacket()
    {
        var bitfield = new Bitfield(20);
        using var pak = PreparedPacket.FromPeerPacket(new BitfieldMessage(bitfield));
        var span = pak.AsSpan();
        Assert.Equal(5 + bitfield.Bytes.Length, span.Length);
        Assert.Equal(1 + bitfield.Bytes.Length, BinaryPrimitives.ReadInt32BigEndian(span));
        Assert.Equal((byte)PeerWireMessageId.Bitfield, span[4]);
        Assert.True(bitfield.Bytes.SequenceEqual(span.Slice(5)));
    }
}
