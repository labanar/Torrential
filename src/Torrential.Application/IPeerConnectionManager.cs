using Torrential.Core;

namespace Torrential.Application;

public interface IPeerConnectionManager
{
    IReadOnlyList<ConnectedPeer> GetConnectedPeers(InfoHash infoHash);
    int GetConnectedPeerCount(InfoHash infoHash);
}

public sealed class ConnectedPeer
{
    public required PeerInfo PeerInfo { get; init; }
    public required PeerId PeerId { get; init; }
    public required InfoHash InfoHash { get; init; }
    public Bitfield? Bitfield { get; set; }
    public long BytesDownloaded { get; set; }
    public long BytesUploaded { get; set; }
    public DateTimeOffset ConnectedAt { get; init; }
}
