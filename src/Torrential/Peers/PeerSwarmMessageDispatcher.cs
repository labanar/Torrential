using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Peers
{
    /// <summary>
    /// Dispatches Have messages to all peers in the swarm when a piece is verified.
    /// Registered as a handler on TorrentEventBus.OnPieceVerified during DI setup.
    /// </summary>
    public sealed class PeerSwarmMessageDispatcher(PeerSwarm peerSwarm, ILogger<PeerSwarmMessageDispatcher> logger)
    {
        public async Task HandlePieceVerified(TorrentPieceVerifiedEvent evt)
        {
            await DispatchHave(evt.InfoHash, evt.PieceIndex);
        }

        public async Task DispatchHave(InfoHash infoHash, int pieceIndex) =>
            await peerSwarm.BroadcastHaveMessage(infoHash, pieceIndex);
    }
}
