namespace Torrential.Application.Peers;

public interface IPeerSwarm
{
    Task MaintainSwarm(InfoHash infoHash);
    Task RemoveSwarm(InfoHash infoHash);
    Task BroadcastHaveMessage(InfoHash infoHash, int pieceIndex);
    Task AddToSwarm(IPeerWireConnection connection);
    IAsyncEnumerable<InfoHash> TrackedTorrents();
    Task<ICollection<PeerWireClient>> GetPeers(InfoHash infoHash);
}
