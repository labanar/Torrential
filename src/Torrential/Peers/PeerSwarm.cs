using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Torrential.Settings;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers
{

    public sealed class PeerSwarm(
        HandshakeService handshakeService,
        TorrentMetadataCache metadataCache,
        TorrentRunner torrentRunner,
        ILogger<PeerSwarm> logger,
        IBus bus,
        SettingsManager settingsManager,
        ILoggerFactory loggerFactory)
    {

        private ConcurrentDictionary<InfoHash, CancellationToken> _swarmCancellations = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> _peerSwarms = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> _swarmTasks = [];

        public IReadOnlyDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> PeerClients => _peerSwarms;
        public IReadOnlyDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> SwarmTasks => _swarmTasks;

        public async Task MaintainSwarm(InfoHash infoHash, CancellationToken stoppingToken)
        {
            if (!metadataCache.TryGet(infoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", infoHash);
                return;
            }

            _swarmCancellations.TryAdd(infoHash, stoppingToken);
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupPeers(stoppingToken);
                //await AnnounceAndFillSwarm(metadata, stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }

        public async Task AddToSwarm(IPeerWireConnection connection)
        {
            if (!metadataCache.TryGet(connection.InfoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", connection.InfoHash);
                return;
            }

            var torrentSettings = await settingsManager.GetDefaultTorrentSettings();
            var globalSettings = await settingsManager.GetGlobalTorrentSettings();
            var peerConnections = _peerSwarms.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());

            if (peerConnections.Count >= torrentSettings.MaxConnections)
            {
                logger.LogInformation("Peer limit reached for {InfoHash}", metadata.InfoHash);
                connection.Dispose();
                return;
            }

            //Check the global limit too accross all torrents
            if (_peerSwarms.Values.Sum(x => x.Count) >= globalSettings.MaxConnections)
            {
                logger.LogInformation("Global peer limit reached");
                connection.Dispose();
                return;
            }

            if (peerConnections.ContainsKey(connection.PeerId.Value)) return;
            var pwcLogger = loggerFactory.CreateLogger<PeerWireClient>();
            var pwc = new PeerWireClient(connection, pwcLogger);
            peerConnections.TryAdd(connection.PeerId.Value, pwc);


            var peerSwarmTasks = _swarmTasks.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, Task>());
            peerSwarmTasks.TryAdd(connection.PeerId.Value, torrentRunner.InitiatePeer(metadata, pwc, CancellationToken.None));
            await bus.Publish(new PeerConnectedEvent
            {
                InfoHash = metadata.InfoHash,
                Ip = connection.PeerInfo.Ip.ToString(),
                Port = connection.PeerInfo.Port,
                PeerId = connection.PeerId.Value
            });
        }

        public async Task<bool> TryAddPeerToSwarm(TorrentMetadata metaData, PeerInfo peerInfo, CancellationToken stoppingToken)
        {
            var torrentSettings = await settingsManager.GetDefaultTorrentSettings();
            var globalSettings = await settingsManager.GetGlobalTorrentSettings();

            var peerConnections = _peerSwarms.GetOrAdd(metaData.InfoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());

            //Check if we're at the peer limit
            if (peerConnections.Count >= torrentSettings.MaxConnections)
            {
                logger.LogInformation("Peer limit reached for {InfoHash}", metaData.InfoHash);
                return false;
            }

            //Check the global limit too accross all torrents
            if (_peerSwarms.Values.Sum(x => x.Count) >= globalSettings.MaxConnections)
            {
                logger.LogInformation("Global peer limit reached");
                return false;
            }

            var infoHash = metaData.InfoHash;
            var timedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timedCts.CancelAfter(5_000);
            var conn = new PeerWireConnection(handshakeService, new TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
            var result = await conn.ConnectOutbound(infoHash, peerInfo, timedCts.Token);

            if (result.Success && conn.PeerId != null)
            {
                var peerSwarmTasks = _swarmTasks.GetOrAdd(metaData.InfoHash, (_) => new ConcurrentDictionary<PeerId, Task>());
                if (peerConnections.ContainsKey(conn.PeerId.Value)) return true;
                var pwcLogger = loggerFactory.CreateLogger<PeerWireClient>();
                var pwc = new PeerWireClient(conn, pwcLogger);
                peerConnections.TryAdd(conn.PeerId.Value, pwc);
                peerSwarmTasks.TryAdd(conn.PeerId.Value, torrentRunner.InitiatePeer(metaData, pwc, stoppingToken));
                await bus.Publish(new PeerConnectedEvent
                {
                    InfoHash = infoHash,
                    Ip = peerInfo.Ip.ToString(),
                    Port = peerInfo.Port,
                    PeerId = conn.PeerId.Value
                });
            }

            //We want to let the consumer know if there's more room for peers, regardless of the outcome of our peer connection
            return true;
        }

        private async Task CleanupPeers(CancellationToken stoppingToken)
        {
            foreach (var (infoHash, swarmTasks) in _swarmTasks)
            {
                foreach (var (peerId, swarmTask) in swarmTasks)
                {
                    if (swarmTask.IsCompleted || swarmTask.IsCanceled || swarmTask.IsFaulted)
                    {
                        logger.LogInformation("Cleaning up peer {PeerId} from swarm {InfoHash}", peerId.ToAsciiString(), infoHash.AsString());
                        //_swarmTasks[infoHash].TryRemove(peerId, out _);
                        //if (_peerSwarms[infoHash].TryRemove(peerId, out var pwc))
                        //    pwc.Dispose();
                    }
                }
            }
        }

        public async Task<ICollection<PeerWireClient>> GetPeers(InfoHash infoHash)
        {
            if (!_peerSwarms.TryGetValue(infoHash, out var peerConnections))
                return Array.Empty<PeerWireClient>();

            return peerConnections.Values.ToArray();
        }
    }

    public class PeerSwarmConfiguration
    {
        public required InfoHash InfoHash { get; init; }
        public required int SwarmSize { get; set; }
    }
}
