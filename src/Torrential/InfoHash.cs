using System.Diagnostics.CodeAnalysis;

namespace Torrential;

public readonly record struct InfoHash(long P1, long P2, int P3) : IParsable<InfoHash>
{
    public static readonly InfoHash None = new InfoHash(long.MaxValue, long.MaxValue, int.MaxValue);

    public static implicit operator InfoHash(string value) => FromHexString(value);
    public static implicit operator string(InfoHash value) => value.AsString();

    public static InfoHash FromSpan(Span<byte> buffer) =>
        new InfoHash(
            buffer.ReadBigEndianInt64(),
            buffer[8..].ReadBigEndianInt64(),
            buffer[16..].ReadBigEndianInt32());


    public static InfoHash FromHexString(ReadOnlySpan<char> hexString)
    {
        if (hexString == null || hexString.Length != 40)
            throw new ArgumentException("Input string must be a valid hexadecimal string and have a length of 40.");

        Span<byte> buffer = new byte[20];
        for (int i = 0; i < hexString.Length; i += 2)
        {
            int highDigit = FromHexChar(hexString[i]);
            int lowDigit = FromHexChar(hexString[i + 1]);
            buffer[i / 2] = (byte)((highDigit << 4) | lowDigit);
        }

        return FromSpan(buffer);
    }

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
            encoded[(i * 3) + 1] = HexDigit(hash[i] >> 4);
            encoded[(i * 3) + 2] = HexDigit(hash[i] & 0x0F);
        }
    }

    private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);
    private static int FromHexChar(char hexChar)
    {
        if (hexChar >= '0' && hexChar <= '9')
            return hexChar - '0';
        else if (hexChar >= 'A' && hexChar <= 'F')
            return 10 + (hexChar - 'A');
        else if (hexChar >= 'a' && hexChar <= 'f')
            return 10 + (hexChar - 'a');
        else
            throw new ArgumentException("Invalid hexadecimal character.");
    }

    public string AsString()
    {
        Span<byte> hash = stackalloc byte[20];
        CopyTo(hash);

        Span<char> encoded = stackalloc char[40];
        for (int i = 0; i < hash.Length; i++)
        {
            encoded[i * 2] = HexDigit(hash[i] >> 4);
            encoded[(i * 2) + 1] = HexDigit(hash[i] & 0x0F);
        }
        return new string(encoded);
    }

    public static InfoHash Parse(string s, IFormatProvider? provider) =>
          FromHexString(s);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out InfoHash result)
    {
        try
        {
            result = FromHexString(s);
            return true;
        }
        catch
        {
            result = None;
            return false;
        }
    }

}

