namespace Torrential.Peers;

public readonly record struct PeerExtensions(byte B1, byte B2, byte B3, byte B4, byte B5, byte B6, byte B7, byte B8)
{
    public static PeerExtensions None => new(0, 0, 0, 0, 0, 0, 0, 0);

    public static PeerExtensions FromLong(long extensions)
    {
        Span<byte> extensionBytes = stackalloc byte[8];
        extensionBytes.TryWriteBigEndian(extensions);
        return new(
            extensionBytes[0],
            extensionBytes[1],
            extensionBytes[2],
            extensionBytes[3],
            extensionBytes[4],
            extensionBytes[5],
            extensionBytes[6],
            extensionBytes[7]);
    }
}
