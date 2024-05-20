using System.Collections.Concurrent;

namespace Torrential.Torrents;

public class TorrentMetadataCache
{
    private readonly ConcurrentDictionary<InfoHash, TorrentMetadata> _torrents = [];
    public void Add(TorrentMetadata meta)
    {
        _torrents.TryAdd(meta.InfoHash, meta);
    }

    public bool TryAdd(TorrentMetadata meta)
    {
        return _torrents.TryAdd(meta.InfoHash, meta);
    }

    public bool TryGet(InfoHash hash, out TorrentMetadata meta)
    {
        return _torrents.TryGetValue(hash, out meta);
    }

    public bool TryRemove(InfoHash hash, out TorrentMetadata meta)
    {
        return _torrents.TryRemove(hash, out meta);
    }
}
