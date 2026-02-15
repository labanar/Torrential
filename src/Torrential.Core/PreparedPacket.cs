using System.Buffers;

namespace Torrential.Core;

public sealed class PreparedPacket : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly byte[] _buffer;
    public readonly int Size;

    public PreparedPacket(int size)
    {
        _pool = ArrayPool<byte>.Shared;
        _buffer = _pool.Rent(size);
        Size = size;
    }

    public void Dispose()
    {
        _pool.Return(_buffer);
    }

    public Span<byte> AsSpan() => _buffer.AsSpan()[..Size];
}
