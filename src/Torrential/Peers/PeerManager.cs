using Microsoft.Extensions.Logging;
using Torrential.Trackers;

namespace Torrential.Peers
{
    public class PeerManager
    {
        private readonly IPeerService _peerService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PeerManager> _logger;
        public Dictionary<InfoHash, List<PeerWireClient>> ConnectedPeers = [];

        public PeerManager(IPeerService peerService, ILoggerFactory loggerFactory, ILogger<PeerManager> logger)
        {
            _peerService = peerService;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public async Task ConnectToPeers(InfoHash infoHash, AnnounceResponse announceResponse, int maxPeers)
        {
            var peerConnectionDict = new Dictionary<PeerWireConnection, Task<PeerConnectionResult>>(announceResponse.Peers.Count);
            for (int i = 0; i < announceResponse.Peers.Count; i++)
            {
                var conn = new PeerWireConnection(_peerService, new System.Net.Sockets.TcpClient(), _loggerFactory.CreateLogger<PeerWireConnection>());
                peerConnectionDict.Add(conn, conn.Connect(infoHash, announceResponse.Peers.ElementAt(i), TimeSpan.FromSeconds(5)));
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

                //Based on the extensions in the handshake, we should know enough to wire up any runtime dependencies
                //1) Piece selection strategy

                numConnected++;
                _logger.LogInformation("Connected to peer");
                if (ConnectedPeers.TryGetValue(infoHash, out var connectedPeers))
                    connectedPeers.Add(new PeerWireClient(conn, _logger));
                else
                    ConnectedPeers[infoHash] = [new PeerWireClient(conn, _logger)];
            }

            peerConnectionDict.Clear();
        }
    }


}
