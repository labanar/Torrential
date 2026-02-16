using System.Buffers;

namespace Torrential.Core.Peers;

/// <summary>
/// A precrafted packet that can be queued and dispatched to the peer
/// 
/// The motiviation here is that we want concurrent writes to the PeerWireClient
/// while the PipeWriter is in the process of writing.
/// </summary>
public sealed class PreparedPacket : IDisposable
{
    public int Size { get; }

    public ReadOnlySpan<byte> PacketData => _buffer.AsSpan()[..Size];

    private readonly byte[] _buffer;
    private readonly ArrayPool<byte> _pool;

    public PreparedPacket(ArrayPool<byte> pool, int size)
    {
        Size = size;
        _pool = pool;
        _buffer = pool.Rent(size);
    }

    public PreparedPacket(int size)
    {
        Size = size;
        _pool = ArrayPool<byte>.Shared;
        _buffer = ArrayPool<byte>.Shared.Rent(size);
    }

    public static PreparedPacket FromPeerPacket<T>(T packet)
        where T : IPeerPacket<T>
    {
        var preparedPacket = new PreparedPacket(packet.MessageSize);
        T.WritePacket(preparedPacket.AsSpan(), packet);
        return preparedPacket;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
    }
    public Span<byte> AsSpan() => _buffer.AsSpan()[..Size];
}
