using System.Collections.Concurrent;

namespace Torrential.Peers;

public sealed class PieceBufferManager
{
    private readonly ConcurrentDictionary<(InfoHash, int pieceIndex), PieceBuffer> _buffers = new();

    public PieceBuffer GetOrCreateBuffer(InfoHash infoHash, int pieceIndex, int pieceLength, PeerId owner)
    {
        var buffer = _buffers.GetOrAdd((infoHash, pieceIndex), _ => new PieceBuffer(pieceIndex, pieceLength, owner));

        if (buffer.Owner != owner)
            buffer.Reassign(owner);

        return buffer;
    }

    public bool TryGetBuffer(InfoHash infoHash, int pieceIndex, out PieceBuffer? buffer)
    {
        return _buffers.TryGetValue((infoHash, pieceIndex), out buffer);
    }

    public void RemoveBuffer(InfoHash infoHash, int pieceIndex)
    {
        if (_buffers.TryRemove((infoHash, pieceIndex), out var buffer))
            buffer.Dispose();
    }

    public void RemoveAllForTorrent(InfoHash infoHash)
    {
        foreach (var kvp in _buffers)
        {
            if (kvp.Key.Item1 == infoHash && _buffers.TryRemove(kvp.Key, out var buffer))
                buffer.Dispose();
        }
    }
}
