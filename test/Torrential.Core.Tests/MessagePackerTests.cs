using System.Buffers;
using Torrential.Core;
using Xunit;

namespace Torrential.Core.Tests;

public class MessagePackerTests
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
        Assert.True(bitfield.Bytes.SequenceEqual(span.Slice(5)));
    }

    [Fact]
    public void Pack_PieceData_Sequence()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var pak = MessagePacker.Pack(1, 2, new ReadOnlySequence<byte>(data));
        var span = pak.AsSpan();
        Assert.Equal(13 + data.Length, span.Length);
        Assert.Equal(PeerWireMessageType.Piece, span[4]);
        Assert.Equal(1, span.Slice(5).ReadBigEndianInt32());
        Assert.Equal(2, span.Slice(9).ReadBigEndianInt32());
        Assert.Equal(data, span.Slice(13).ToArray());
    }

    [Fact]
    public void Pack_Bitfield_Sequence()
    {
        var bitfield = new Bitfield(20);
        using var pak = MessagePacker.PackBitfield(bitfield.Bytes);
        var span = pak.AsSpan();

        Assert.Equal(8, span.Length);
        Assert.Equal(PeerWireMessageType.Bitfield, span[4]);
        Assert.True(bitfield.Bytes.SequenceEqual(span.Slice(5)));
    }

    [Fact]
    public void Pack_KeepAlive()
    {
        // Keep-alive is 4 zero bytes (message length = 0, no message id, no payload)
        Span<byte> keepAlive = stackalloc byte[4];
        keepAlive.TryWriteBigEndian(0);

        Assert.Equal(0, keepAlive[0]);
        Assert.Equal(0, keepAlive[1]);
        Assert.Equal(0, keepAlive[2]);
        Assert.Equal(0, keepAlive[3]);
        Assert.Equal(0, keepAlive.ReadBigEndianInt32());
    }

    [Fact]
    public void PreparedPacket_Returns_Buffer_To_Pool_On_Dispose()
    {
        var pak = new PreparedPacket(10);
        var span = pak.AsSpan();
        Assert.Equal(10, span.Length);

        // Write some data to verify the packet works
        span[0] = 0xFF;
        Assert.Equal(0xFF, pak.AsSpan()[0]);

        // Should not throw - buffer is returned to pool
        pak.Dispose();
    }

    [Fact]
    public void PieceRequestMessage_FromReadOnlySequence_RoundTrip()
    {
        // Create a piece request: pieceIndex=5, begin=16384, length=16384
        var pieceIndex = 5;
        var begin = 16384;
        var length = 16384;

        // Serialize to bytes (big-endian)
        var data = new byte[12];
        Span<byte> dataSpan = data;
        dataSpan.TryWriteBigEndian(pieceIndex);
        dataSpan[4..].TryWriteBigEndian(begin);
        dataSpan[8..].TryWriteBigEndian(length);

        // Parse back
        var sequence = new ReadOnlySequence<byte>(data);
        var msg = PieceRequestMessage.FromReadOnlySequence(sequence);

        Assert.Equal(pieceIndex, msg.PieceIndex);
        Assert.Equal(begin, msg.Begin);
        Assert.Equal(length, msg.Length);
    }
}
