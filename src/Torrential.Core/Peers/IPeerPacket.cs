using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Torrential.Core.Peers;

public interface IPeerPacket<T>
    where T : IPeerPacket<T>, allows ref struct
{
    int MessageSize { get; }
    static abstract PeerWireMessageId MessageId { get; }
    static abstract void WritePacket(Span<byte> buffer, T message);
    static virtual void WritePacket(PipeWriter writer, T message)
    {
        var buff = writer.GetSpan(message.MessageSize + 4);
        T.WritePacket(buff, message);
    }
}

public interface IPeerActionPacket<T> : IPeerPacket<T>
    where T : IPeerActionPacket<T>, allows ref struct
{
    static void IPeerPacket<T>.WritePacket(Span<byte> buffer, T message)
    {
        BinaryPrimitives.WriteInt32BigEndian(buffer, 1);
        buffer[4] = (byte)T.MessageId;
    }
}



public interface IPreparedPacket<T> : IPeerPacket<T>
    where T : IPreparedPacket<T>
{
    ReadOnlySpan<byte> PacketData { get; }
}

public abstract class PreparedPeerPacket<T> : IDisposable, IPeerPacket<T>, IPreparedPacket<T>
    where T : IPreparedPacket<T>
{
    public int Size { get; }

    public ReadOnlySpan<byte> PacketData => _buffer.AsSpan()[..Size];

    public int MessageSize => Size;
    public static PeerWireMessageId MessageId => T.MessageId;

    protected readonly byte[] _buffer;
    private readonly ArrayPool<byte> _pool;

    public PreparedPeerPacket(ArrayPool<byte> pool, int size)
    {
        Size = size;
        _pool = pool;
        _buffer = pool.Rent(size);
    }

    public PreparedPeerPacket(int size)
    {
        Size = size;
        _pool = ArrayPool<byte>.Shared;
        _buffer = ArrayPool<byte>.Shared.Rent(size);
    }

    public static PreparedPacket FromPeerPacket(T packet)
    {
        var preparedPacket = new PreparedPacket(packet.MessageSize + 4);
        T.WritePacket(preparedPacket.AsSpan(), packet);
        return preparedPacket;
    }

    public void Dispose()
    {
        _pool.Return(_buffer);
    }
    public Span<byte> AsSpan() => _buffer.AsSpan()[..Size];

    public static void WritePacket(Span<byte> buffer, T message)
    {
        message.PacketData.CopyTo(buffer);
    }
}
