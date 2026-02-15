using Torrential.Core;
using Xunit;

namespace Torrential.Core.Tests;

public class BitConverterExtensionTests
{
    [Fact]
    public void Write_Int32()
    {
        Span<byte> buffer = stackalloc byte[4];
        buffer.TryWriteBigEndian(40);
        Span<byte> expected = [0x00, 0x00, 0x00, 0x28];
        Assert.True(buffer.SequenceEqual(expected));
    }

    [Fact]
    public void Write_Int64()
    {
        Span<byte> buffer = stackalloc byte[8];
        buffer.TryWriteBigEndian(40l);
        Span<byte> expected = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28];
        Assert.True(buffer.SequenceEqual(expected));
    }

    [Fact]
    public void Read_Int32()
    {
        Span<byte> buffer = [0x00, 0x00, 0x00, 0x28];
        var expected = 40;
        Assert.Equal(buffer.ReadBigEndianInt32(), expected);
        Assert.True(buffer.SequenceEqual(new byte[] { 0x00, 0x00, 0x00, 0x28 }));
    }

    [Fact]
    public void Read_UInt16()
    {
        Span<byte> buffer = [0x00, 0x28];
        ushort expected = 40;
        Assert.Equal(buffer.ReadBigEndianUInt16(), expected);
        Assert.True(buffer.SequenceEqual(new byte[] { 0x00, 0x28 }));
    }
}
