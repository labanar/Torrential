namespace Torrential.Peers;

internal static class PeerWireMessageType
{
    public const byte Choke = 0;
    public const byte Unchoke = 1;
    public const byte Interested = 2;
    public const byte NotInterested = 3;
    public const byte Have = 4;
    public const byte Bitfield = 5;
    public const byte Request = 6;
    public const byte Piece = 7;
    public const byte Cancel = 8;
    public const byte Port = 9;
}

public enum PeerWireMessageId : byte
{
    Choke = 0,
    Unchoke = 1,
    Interested = 2,
    NotInterested = 3,
    Have = 4,
    Bitfield = 5,
    Request = 6,
    Piece = 7,
    Cancel = 8,
    Port = 9
}
