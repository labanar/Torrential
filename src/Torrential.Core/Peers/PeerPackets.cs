using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Torrential.Core.Peers;





[InlineArray(68)]
public struct HandshakeData
{
    private byte _element0;

    private static byte[] EMPTY_RESERVED = [0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
    private static ReadOnlySpan<byte> PROTOCOL_BYTES => "BitTorrent protocol"u8;

    public static HandshakeData Create(ReadOnlySpan<byte> infoHash, ReadOnlySpan<byte> peerId)
    {
        Span<byte> data = stackalloc byte[68];
        data[0] = 19;
        PROTOCOL_BYTES.Slice(0, 19).CopyTo(data.Slice(1, 19));
        EMPTY_RESERVED.CopyTo(data.Slice(20, 8));
        infoHash.CopyTo(data.Slice(28, 20));
        peerId.CopyTo(data.Slice(48, 20));
        var handshakeData = new HandshakeData();
        data.CopyTo(handshakeData);
        return handshakeData;
    }
}

public class BitfieldMessage(byte[] data) : IPeerPacket<BitfieldMessage>
{
    public static PeerWireMessageId MessageId => PeerWireMessageId.Bitfield;

    public int MessageSize => data.Length;

    public static void WritePacket(Span<byte> buffer, BitfieldMessage message)
    {
        throw new NotImplementedException();
    }
}

public readonly ref struct ChokeMessage : IPeerActionPacket<ChokeMessage>
{
    public int MessageSize => 5;
    public static PeerWireMessageId MessageId => PeerWireMessageId.Choke;

}

public readonly ref struct UnchokeMessage : IPeerActionPacket<UnchokeMessage>
{
    public int MessageSize => 5;
    public static PeerWireMessageId MessageId => PeerWireMessageId.Unchoke;
}

public readonly ref struct InterestedMessage : IPeerActionPacket<InterestedMessage>
{
    public int MessageSize => 5;
    public static PeerWireMessageId MessageId => PeerWireMessageId.Interested;
}

public readonly ref struct NotInterestedMessage : IPeerActionPacket<NotInterestedMessage>
{
    public int MessageSize => 5;
    public static PeerWireMessageId MessageId => PeerWireMessageId.NotInterested;
}

public readonly ref struct HaveMessage(int index) : IPeerPacket<HaveMessage>
{
    public readonly int Index => index;
    public int MessageSize => 9;
    public static PeerWireMessageId MessageId => PeerWireMessageId.Have;
    private static HaveMessage Default => new HaveMessage(0);
    public bool TryReadPacketData(ReadOnlySequence<byte> payload, out HaveMessage message)
    {
        message = Default;
        var sequenceReader = new SequenceReader<byte>(payload);
        if (!sequenceReader.TryReadBigEndian(out int index))
            return false;

        message = new HaveMessage(index);
        return true;
    }

    public static void WritePacket(Span<byte> buffer, HaveMessage pak)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer, 5);
        buffer[4] = (byte)MessageId;
        BinaryPrimitives.WriteInt32BigEndian(buffer[5..], pak.Index);
    }
}

public ref struct PieceRequestMessage : IPeerPacket<PieceRequestMessage>
{
    public static PeerWireMessageId MessageId => PeerWireMessageId.Request;
    public int Index { get; }
    public int Begin { get; }
    public int Length { get; }
    public int MessageSize => 13;

    public PieceRequestMessage(int index, int begin, int length)
    {
        Index = index;
        Begin = begin;
        Length = length;
    }
    public static void WritePacket(Span<byte> buffer, PieceRequestMessage message)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer, 13);
        buffer[4] = (byte)MessageId;
        BinaryPrimitives.WriteInt32BigEndian(buffer[5..], message.Index);
        BinaryPrimitives.WriteInt32BigEndian(buffer[9..], message.Begin);
        BinaryPrimitives.WriteInt32BigEndian(buffer[13..], message.Length);
    }
}

public class PieceMessage : IPeerPacket<PieceMessage>
{
    public static PeerWireMessageId MessageId => PeerWireMessageId.Piece;
    public int Index { get; }
    public int Begin { get; }
    public int Length { get; }
    public ReadOnlySequence<byte> Data { get; }
    public int MessageSize => 13 + (int)Data.Length;

    public static void WritePacket(Span<byte> buffer, PieceMessage message)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer, message.MessageSize);
        buffer[4] = (byte)MessageId;
        BinaryPrimitives.WriteInt32BigEndian(buffer[5..], message.Index);
        BinaryPrimitives.WriteInt32BigEndian(buffer[9..], message.Begin);
        BinaryPrimitives.WriteInt32BigEndian(buffer[13..], message.Length);
        message.Data.CopyTo(buffer[17..]);
    }
}

public class PreparedPieceMessage : PreparedPeerPacket<PreparedPieceMessage>
{
    public int Index { get; }
    public int Begin { get; }
    public int Length { get; }
    public PreparedPieceMessage(ArrayPool<byte> pool, int index, int begin, int length) : base(pool, 13 + length)
    {
        BinaryPrimitives.WriteInt32BigEndian(_buffer, 13 + length);
        _buffer[4] = (byte)MessageId;
        BinaryPrimitives.WriteInt32BigEndian(_buffer[5..], index);
        BinaryPrimitives.WriteInt32BigEndian(_buffer[9..], begin);
        BinaryPrimitives.WriteInt32BigEndian(_buffer[13..], length);
    }
}


public readonly ref struct CancelMessage : IPeerPacket<CancelMessage>
{
    public static PeerWireMessageId MessageId => PeerWireMessageId.Cancel;
    public int Index { get; }
    public int Begin { get; }
    public int Length { get; }
    public int MessageSize => 13;
    public CancelMessage(int index, int begin, int length)
    {
        Index = index;
        Begin = begin;
        Length = length;
    }

    public static void WritePacket(Span<byte> buffer, CancelMessage message)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer, 13);
        buffer[4] = (byte)MessageId;
        BinaryPrimitives.WriteInt32BigEndian(buffer[5..], message.Index);
        BinaryPrimitives.WriteInt32BigEndian(buffer[9..], message.Begin);
        BinaryPrimitives.WriteInt32BigEndian(buffer[13..], message.Length);
    }
}



