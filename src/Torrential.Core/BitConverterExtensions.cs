using System.Buffers;
using System.Buffers.Binary;

namespace Torrential.Core;

public static class BitConverterExtensions
{
    public static ushort ReadBigEndianUInt16(this Span<byte> buffer)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public static ushort ReadBigEndianUInt16(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[2];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public static int ReadBigEndianInt32(this Span<byte> buffer)
    {
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public static int ReadBigEndianInt32(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[4];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public static long ReadBigEndianInt64(this Span<byte> buffer)
    {
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public static long ReadBigEndianInt64(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[8];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, int value)
    {
        if (buffer.Length < 4)
            return false;

        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        return true;
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, long value)
    {
        if (buffer.Length < 8)
            return false;

        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        return true;
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, uint value)
    {
        if (buffer.Length < 4)
            return false;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        return true;
    }
}
