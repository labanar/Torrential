using System.Buffers;

namespace Torrential;

public static class BitConverterExtensions
{
    public static ushort ReadBigEndianUInt16(this Span<byte> buffer)
    {
        if (BitConverter.IsLittleEndian)
            buffer[..2].Reverse();

        var value = BitConverter.ToUInt16(buffer[..2]);

        if (BitConverter.IsLittleEndian)
            buffer[..2].Reverse();

        return value;
    }

    public static ushort ReadBigEndianUInt16(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[2];
        sequence.CopyTo(buffer);
        return buffer.ReadBigEndianUInt16();
    }


    public static int ReadBigEndianInt32(this Span<byte> buffer)
    {
        if (BitConverter.IsLittleEndian)
            buffer[..4].Reverse();

        var value = BitConverter.ToInt32(buffer[..4]);

        if (BitConverter.IsLittleEndian)
            buffer[..4].Reverse();

        return value;
    }

    public static int ReadBigEndianInt32(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[4];
        sequence.CopyTo(buffer);
        return buffer.ReadBigEndianInt32();
    }

    public static long ReadBigEndianInt64(this Span<byte> buffer)
    {
        if (BitConverter.IsLittleEndian)
            buffer[..8].Reverse();

        var value = BitConverter.ToInt64(buffer[..8]);

        if (BitConverter.IsLittleEndian)
            buffer[..8].Reverse();

        return value;
    }

    public static long ReadBigEndianInt64(this ReadOnlySequence<byte> sequence)
    {
        Span<byte> buffer = stackalloc byte[8];
        sequence.CopyTo(buffer);
        return buffer.ReadBigEndianInt64();
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, int value)
    {
        if (buffer.Length < 4)
            return false;

        if (!BitConverter.TryWriteBytes(buffer[..4], value))
            return false;

        if (BitConverter.IsLittleEndian)
            buffer[..4].Reverse();

        return true;
    }

    public static bool TryWriteBigEndian(this Span<byte> buffer, long value)
    {
        if (buffer.Length < 8)
            return false;

        if (!BitConverter.TryWriteBytes(buffer[..8], value))
            return false;

        if (BitConverter.IsLittleEndian)
            buffer[..8].Reverse();

        return true;
    }


    public static bool TryWriteBigEndian(this Span<byte> buffer, uint value)
    {
        if (buffer.Length < 4)
            return false;

        if (!BitConverter.TryWriteBytes(buffer[..4], value))
            return false;

        if (BitConverter.IsLittleEndian)
            buffer[..4].Reverse();

        return true;
    }
}
