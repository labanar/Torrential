using System.Collections.Concurrent;
using Torrential.Core;

namespace Torrential.Application;

public record DiscoveredPeer(PeerInfo PeerInfo, DateTimeOffset DiscoveredAt, string? Source);

public interface IPeerPool
{
    void AddPeers(InfoHash infoHash, IEnumerable<PeerInfo> peers, string? source = null);
    IReadOnlyList<DiscoveredPeer> GetPeers(InfoHash infoHash);
    int GetPeerCount(InfoHash infoHash);
    void RemoveTorrent(InfoHash infoHash);
}

public class PeerPool : IPeerPool
{
    private readonly ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerInfo, DiscoveredPeer>> _peers = new();

    public void AddPeers(InfoHash infoHash, IEnumerable<PeerInfo> peers, string? source = null)
    {
        var torrentPeers = _peers.GetOrAdd(infoHash, _ => new ConcurrentDictionary<PeerInfo, DiscoveredPeer>());
        var now = DateTimeOffset.UtcNow;

        foreach (var peer in peers)
        {
            torrentPeers.TryAdd(peer, new DiscoveredPeer(peer, now, source));
        }
    }

    public IReadOnlyList<DiscoveredPeer> GetPeers(InfoHash infoHash)
    {
        if (_peers.TryGetValue(infoHash, out var torrentPeers))
            return torrentPeers.Values.ToList();

        return [];
    }

    public int GetPeerCount(InfoHash infoHash)
    {
        if (_peers.TryGetValue(infoHash, out var torrentPeers))
            return torrentPeers.Count;

        return 0;
    }

    public void RemoveTorrent(InfoHash infoHash)
    {
        _peers.TryRemove(infoHash, out _);
    }
}
