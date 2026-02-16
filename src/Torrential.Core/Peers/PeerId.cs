using System.Text;
using Torrential.Core.Utils;

namespace Torrential.Core.Peers;
public readonly record struct PeerId(long P1, long P2, int P3)
{
    private static readonly ushort TorrentialImplementation = CreateImplementationShort('T', 'O');
    public static PeerId None { get; } = new(long.MaxValue, long.MaxValue, int.MaxValue);

    public static explicit operator string(PeerId value) => value.ToAsciiString();

    public static PeerId From(ReadOnlySpan<byte> span)
    {
        return new(span.ReadBigEndianInt64(), span[8..].ReadBigEndianInt64(), span[16..].ReadBigEndianInt32());
    }

    public static PeerId From(Span<byte> buffer) =>
        new(buffer.ReadBigEndianInt64(), buffer[8..].ReadBigEndianInt64(), buffer[16..].ReadBigEndianInt32());

    public static PeerId New =>
        FromConvention(TorrentialImplementation, '0', '1', '0', '0');

    public static PeerId WithPrefix(ReadOnlySpan<byte> prefix)
    {
        Span<byte> buffer = stackalloc byte[20];
        prefix[0..Math.Min(prefix.Length, 20)].CopyTo(buffer);

        var bytesToFill = 20 - Math.Min(20, prefix.Length);
        if (bytesToFill != 0)
            Random.Shared.NextBytes(buffer[bytesToFill..]);

        return PeerId.From(buffer);
    }

    public string ToAsciiString()
    {
        Span<byte> buffer = stackalloc byte[20];
        buffer.TryWriteBigEndian(P1);
        buffer[8..].TryWriteBigEndian(P2);
        buffer[16..].TryWriteBigEndian(P3);
        return Encoding.ASCII.GetString(buffer);
    }

    public void CopyTo(Span<byte> buffer)
    {
        buffer.TryWriteBigEndian(P1);
        buffer[8..].TryWriteBigEndian(P2);
        buffer[16..].TryWriteBigEndian(P3);
    }

    public bool FollowsConvention()
    {
        Span<byte> buffer = stackalloc byte[8];
        buffer.TryWriteBigEndian(P1);
        return buffer switch
        {
            [(byte)'-', var cli1, var cli2, var v1, var v2, var v3, var v4, (byte)'-', _]
                when
                    IsChar(cli1)
                    && IsChar(cli2)
                    && IsValidVersionByte(v1)
                    && IsValidVersionByte(v2)
                    && IsValidVersionByte(v3)
                    && IsValidVersionByte(v4)
              => true,
            _ => false
        };
    }

    private static bool IsChar(byte b)
    {
        if (b >= 'A' && b <= 'Z') return true;
        if (b >= 'a' && b <= 'z') return true;
        return false;
    }
    private static bool IsDigit(byte b)
    {
        if (b >= '0' && b <= '9') return true;
        return false;
    }
    private static bool IsValidVersionByte(byte b)
    {
        if (IsChar(b)) return true;
        if (IsDigit(b)) return true;
        if (b == '.') return true;
        if (b == '-') return true;
        return false;

    }

    private static ushort CreateImplementationShort(char a, char b) => (ushort)(((byte)a << 8) | (byte)b);
    private static PeerId FromConvention(ushort implementation, char v1, char v2, char v3, char v4)
    {
        Span<byte> p1Span = stackalloc byte[8]
        {
            (byte)'-',
            (byte)(implementation >> 8),
            (byte)(implementation & 0xff),
            (byte)v1,
            (byte)v2,
            (byte)v3,
            (byte)v4,
            (byte)'-'
        };

        Span<byte> p2Span = stackalloc byte[8]
        {
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
        };

        Span<byte> p3Span = stackalloc byte[4]
        {
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
            (byte)(char)Random.Shared.Next('0', '9' + 1),
        };

        return new PeerId(p1Span.ReadBigEndianInt64(), p2Span.ReadBigEndianInt64(), p3Span.ReadBigEndianInt32());
    }
}
