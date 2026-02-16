using System.Buffers;
using System.Buffers.Binary;

namespace Torrential.Core.Utils;

public static class BitConverterExtensions
{
    public static ushort ReadBigEndianUInt16(this Span<byte> buffer)
        => BinaryPrimitives.ReadUInt16BigEndian(buffer);

    public static ushort ReadBigEndianUInt16(this ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadUInt16BigEndian(buffer);

    public static ushort ReadBigEndianUInt16(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[2];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public static int ReadBigEndianInt32(this Span<byte> buffer)
        => BinaryPrimitives.ReadInt32BigEndian(buffer);

    public static int ReadBigEndianInt32(this ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadInt32BigEndian(buffer);

    public static int ReadBigEndianInt32(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[4];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public static long ReadBigEndianInt64(this Span<byte> buffer)
        => BinaryPrimitives.ReadInt64BigEndian(buffer);

    public static long ReadBigEndianInt64(this ReadOnlySpan<byte> buffer)
        => BinaryPrimitives.ReadInt64BigEndian(buffer);

    public static long ReadBigEndianInt64(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[8];
        sequence.CopyTo(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, int value)
        => BinaryPrimitives.TryWriteInt32BigEndian(buffer, value);

    public static bool TryWriteBigEndian(this Span<byte> buffer, long value)
        => BinaryPrimitives.TryWriteInt64BigEndian(buffer, value);

    public static bool TryWriteBigEndian(this Span<byte> buffer, uint value)
        => BinaryPrimitives.TryWriteUInt32BigEndian(buffer, value);
}
