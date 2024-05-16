using System.Buffers;

namespace Torrential.Peers;

public static class MessagePacker
{
    public static PreparedPacket Pack(byte messageId)
    {
        var pak = new PreparedPacket(5);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(1);
        buffer[4] = messageId;
        return pak;
    }

    public static PreparedPacket Pack(byte messageId, int p1)
    {
        var pak = new PreparedPacket(9);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(5);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        return pak;
    }

    public static PreparedPacket Pack(byte messageId, int p1, int p2)
    {
        var pak = new PreparedPacket(13);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(9);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        buffer[9..].TryWriteBigEndian(p2);
        return pak;
    }

    public static PreparedPacket Pack(byte messageId, int p1, int p2, int p3)
    {
        var pak = new PreparedPacket(17);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(13);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        buffer[9..].TryWriteBigEndian(p2);
        buffer[13..].TryWriteBigEndian(p3);
        return pak;
    }

    public static PreparedPacket Pack(int pieceIndex, int begin, ReadOnlySequence<byte> pieceData)
    {
        var pak = new PreparedPacket(13 + (int)pieceData.Length);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(9 + (int)pieceData.Length);
        buffer[4] = PeerWireMessageType.Piece;
        buffer[5..].TryWriteBigEndian(pieceIndex);
        buffer[9..].TryWriteBigEndian(begin);
        pieceData.CopyTo(buffer.Slice(13));
        return pak;
    }

    public static PreparedPacket Pack(int pieceIndex, int begin, ReadOnlySpan<byte> pieceData)
    {
        var pak = new PreparedPacket(13 + pieceData.Length);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(9 + pieceData.Length);
        buffer[4] = PeerWireMessageType.Piece;
        buffer[5..].TryWriteBigEndian(pieceIndex);
        buffer[9..].TryWriteBigEndian(begin);
        pieceData.CopyTo(buffer.Slice(13));
        return pak;
    }


    public static PreparedPacket PackBitfield(ReadOnlySequence<byte> bitfieldData)
    {
        var bitfieldLength = (int)bitfieldData.Length;
        var pak = new PreparedPacket(5 + bitfieldLength);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(bitfieldLength + 1);
        buffer[4] = PeerWireMessageType.Bitfield;
        bitfieldData.CopyTo(buffer[5..]);
        return pak;
    }

    public static PreparedPacket Pack(IBitfield bitfield)
    {
        var pak = new PreparedPacket(bitfield.Bytes.Length + 5);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(bitfield.Bytes.Length + 1);
        buffer[4] = PeerWireMessageType.Bitfield;
        bitfield.Bytes.CopyTo(buffer[5..]);
        return pak;
    }

    public static PreparedPacket? Pack(int messageSize, byte messageId, ReadOnlySequence<byte> payload)
    {
        switch (messageId)
        {
            case PeerWireMessageType.Choke:
            case PeerWireMessageType.Unchoke:
            case PeerWireMessageType.Interested:
            case PeerWireMessageType.NotInterested:
                return Pack(messageId);
            case PeerWireMessageType.Have:
                return Pack(messageId, payload.Slice(0, 4).ReadBigEndianInt32());
            case PeerWireMessageType.Request:
                return Pack(messageId, payload.Slice(0, 4).ReadBigEndianInt32(), payload.Slice(4, 4).ReadBigEndianInt32(), payload.Slice(8, 4).ReadBigEndianInt32());
            case PeerWireMessageType.Piece:
                return Pack(payload.Slice(0, 4).ReadBigEndianInt32(), payload.Slice(4, 4).ReadBigEndianInt32(), payload.Slice(8));
            case PeerWireMessageType.Bitfield:
                return PackBitfield(payload);
            default:
                return null;
        }
    }
}
