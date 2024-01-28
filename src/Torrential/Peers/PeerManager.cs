using MassTransit;
using Microsoft.Extensions.Logging;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers
{
    public class PeerManager(IPeerService peerService, ILoggerFactory loggerFactory, ILogger<PeerManager> logger, IBus bus)
    {
        public Dictionary<InfoHash, List<PeerWireClient>> ConnectedPeers = [];

        public async Task ConnectToPeers(InfoHash infoHash, AnnounceResponse announceResponse, int maxPeers, CancellationToken cancellationToken)
        {
            var timedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timedCts.CancelAfter(5_000);

            var peerConnectionDict = new Dictionary<PeerWireConnection, Task<PeerConnectionResult>>(announceResponse.Peers.Count);
            for (int i = 0; i < announceResponse.Peers.Count; i++)
            {
                var conn = new PeerWireConnection(peerService, new System.Net.Sockets.TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
                peerConnectionDict.Add(conn, conn.Connect(infoHash, announceResponse.Peers.ElementAt(i), timedCts.Token));
            }
            await Task.WhenAll(peerConnectionDict.Values);

            var numConnected = 0;
            foreach (var (conn, connectTask) in peerConnectionDict)
            {
                var result = await connectTask;
                if (!result.Success || numConnected >= maxPeers)
                {
                    conn.Dispose();
                    continue;
                }

                await bus.Publish(new PeerConnectedEvent { InfoHash = infoHash }, cancellationToken);

                //Based on the extensions in the handshake, we should know enough to wire up any runtime dependencies
                //1) Piece selection strategy

                numConnected++;
                logger.LogInformation("Connected to peer");
                if (ConnectedPeers.TryGetValue(infoHash, out var connectedPeers))
                    connectedPeers.Add(new PeerWireClient(conn, logger));
                else
                    ConnectedPeers[infoHash] = [new PeerWireClient(conn, logger)];
            }

            peerConnectionDict.Clear();
        }
    }


}
