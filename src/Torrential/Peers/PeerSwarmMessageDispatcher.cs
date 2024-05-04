using MassTransit;
using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Peers
{
    /// <summary>
    /// Should be responsible for the following:
    /// 
    /// 1) Re-announcing on some cadence
    /// 2) Keeping the swarm healthy (removing bad peers, connecting to new ones when we're under the limit)
    /// 3) Central place for us to dispatch have messages to peers (once we downlod a piece we need to inform the peers we have it now)
    /// 
    /// </summary>
    /// 
    public sealed class PeerSwarmMessageDispatcher(PeerSwarm peerSwarm, ILogger<PeerSwarmMessageDispatcher> logger) : IConsumer<TorrentPieceVerifiedEvent>

    {
        public async Task Consume(ConsumeContext<TorrentPieceVerifiedEvent> context)
        {
            var request = context.Message;
            await DispatchHave(request.InfoHash, request.PieceIndex);
        }

        public async Task DispatchHave(InfoHash infoHash, int pieceIndex)
        {
            if (!peerSwarm.PeerClients.TryGetValue(infoHash, out var peerClients))
            {
                logger.LogWarning("No peers found for {InfoHash}", infoHash);
                return;
            }

            var tasks = new List<Task>();
            try
            {
                foreach (var peer in peerClients.Values)
                    tasks.Add(peer.SendHave(pieceIndex));

                await Task.WhenAll(tasks);
            }
            finally
            {
            }
        }
    }
}
