using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Torrential.Core.Torrents;

[InlineArray(20)]
public struct InfoHashData
{
    private byte _element0;

    public static InfoHashData FromSpan(ReadOnlySpan<byte> buffer)
    {
        var data = new InfoHashData();
        buffer.CopyTo(data);
        return data;
    }
}

public readonly record struct InfoHash(InfoHashData data)
{
    private static ConcurrentDictionary<InfoHash, string> _stringCache = new();

    public static readonly InfoHash None = new InfoHash(new InfoHashData());

    public static implicit operator InfoHash(string value) => FromHexString(value);
    public static implicit operator string(InfoHash value) => value.AsString();
    public static implicit operator InfoHash(byte[] value) => new(InfoHashData.FromSpan(value));
    public static implicit operator InfoHash(ReadOnlySpan<byte> value) => new(InfoHashData.FromSpan(value));

    public static InfoHash FromHexString(ReadOnlySpan<char> hexString)
    {
        if (hexString.IsEmpty || hexString.Length != 40)
            throw new ArgumentException("Input string must be a valid hexadecimal string and have a length of 40.");

        Span<byte> buffer = new byte[20];
        for (int i = 0; i < hexString.Length; i += 2)
        {
            int highDigit = FromHexChar(hexString[i]);
            int lowDigit = FromHexChar(hexString[i + 1]);
            buffer[i / 2] = (byte)((highDigit << 4) | lowDigit);
        }

        return new(InfoHashData.FromSpan(buffer));
    }

    public void WriteUrlEncodedHash(Span<char> destination)
    {
        UrlEncodeHash(data, destination);
    }

    private static void UrlEncodeHash(InfoHashData data, Span<char> encoded)
    {
        for (int i = 0; i < 20; i++)
        {
            encoded[i * 3] = '%';
            encoded[(i * 3) + 1] = HexDigit(data[i] >> 4);
            encoded[(i * 3) + 2] = HexDigit(data[i] & 0x0F);
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
        //Check the cache first, to avoid the allocation
        if (_stringCache.TryGetValue(this, out var cached))
            return cached;

        Span<char> encoded = stackalloc char[40];
        for (int i = 0; i < 20; i++)
        {
            encoded[i * 2] = HexDigit(data[i] >> 4);
            encoded[(i * 2) + 1] = HexDigit(data[i] & 0x0F);
        }
        var str = new string(encoded);
        _stringCache.TryAdd(this, str);
        return str;
    }

    public void CopyHexString(Span<char> destination)
    {
        Span<char> encoded = stackalloc char[40];
        for (int i = 0; i < 20; i++)
        {
            encoded[i * 2] = HexDigit(data[i] >> 4);
            encoded[(i * 2) + 1] = HexDigit(data[i] & 0x0F);
        }

        encoded.CopyTo(destination);
    }
}
