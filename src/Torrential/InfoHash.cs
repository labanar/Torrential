namespace Torrential;

public readonly record struct InfoHash(long P1, long P2, int P3)
{
    public static readonly InfoHash None = new InfoHash(long.MaxValue, long.MaxValue, int.MaxValue);

    public static InfoHash FromSpan(Span<byte> buffer) =>
        new InfoHash(
            buffer.ReadBigEndianInt64(),
            buffer[8..].ReadBigEndianInt64(),
            buffer[16..].ReadBigEndianInt32());

    public void CopyTo(Span<byte> buffer)
    {
        buffer.TryWriteBigEndian(P1);
        buffer[8..].TryWriteBigEndian(P2);
        buffer[16..].TryWriteBigEndian(P3);
    }

    public void WriteUrlEncodedHash(Span<char> destination)
    {
        Span<byte> buffer = stackalloc byte[20];
        CopyTo(buffer);
        UrlEncodeHash(buffer, destination);
    }

    private static void UrlEncodeHash(Span<byte> hash, Span<char> encoded)
    {
        for (int i = 0; i < hash.Length; i++)
        {
            encoded[i * 3] = '%';
            encoded[i * 3 + 1] = HexDigit(hash[i] >> 4);
            encoded[i * 3 + 2] = HexDigit(hash[i] & 0x0F);
        }
    }

    private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
}

