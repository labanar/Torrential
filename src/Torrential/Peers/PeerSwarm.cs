using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers
{

    public sealed class PeerSwarm(
        BitfieldManager bitfields,
        HandshakeService handshakeService,
        TorrentMetadataCache metadataCache,
        TorrentRunner torrentRunner,
        IPeerService peerService,
        IEnumerable<ITrackerClient> trackerClients,
        ILogger<PeerSwarm> logger,
        IBus bus,
        ILoggerFactory loggerFactory)
    {
        private ConcurrentDictionary<InfoHash, PeerSwarmConfiguration> _swarmConfigs = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> _peerSwarms = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> _swarmTasks = [];

        public IReadOnlyDictionary<InfoHash, PeerSwarmConfiguration> SwarmConfigs => _swarmConfigs;
        public IReadOnlyDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> PeerClients => _peerSwarms;
        public IReadOnlyDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> SwarmTasks => _swarmTasks;


        public async Task MaintainSwarm(InfoHash infoHash, int swarmSize, CancellationToken stoppingToken)
        {
            if (!metadataCache.TryGet(infoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", infoHash);
                return;
            }

            if (_swarmConfigs.TryGetValue(infoHash, out var swarmConfig))
            {
                swarmConfig.SwarmSize = swarmSize;
            }

            _swarmConfigs.TryAdd(infoHash, new PeerSwarmConfiguration
            {
                InfoHash = infoHash,
                SwarmSize = swarmSize
            });
            _swarmTasks.TryAdd(infoHash, new ConcurrentDictionary<PeerId, Task>());

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupPeers(stoppingToken);
                await AnnounceAndFillSwarm(metadata, stoppingToken);
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

            var peerConnections = _peerSwarms.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());

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

        private async Task AnnounceAndFillSwarm(TorrentMetadata metadata, CancellationToken stoppingToken)
        {
            var peerTasks = new List<Task>();
            await foreach (var announceResponse in Announce(metadata))
            {
                foreach (var peer in announceResponse.Peers)
                {
                    peerTasks.Add(TryAddPeerToSwarm(metadata, peer, stoppingToken));
                }
            }
            await Task.WhenAll(peerTasks);
        }


        public async Task<bool> TryAddPeerToSwarm(TorrentMetadata metaData, PeerInfo peerInfo, CancellationToken stoppingToken)
        {
            var infoHash = metaData.InfoHash;
            var timedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timedCts.CancelAfter(5_000);

            var conn = new PeerWireConnection(handshakeService, new TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
            var result = await conn.ConnectOutbound(infoHash, peerInfo, timedCts.Token);

            if (result.Success && conn.PeerId != null)
            {
                var peerConnections = _peerSwarms.GetOrAdd(infoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());
                if (peerConnections.ContainsKey(conn.PeerId.Value)) return true;
                var pwcLogger = loggerFactory.CreateLogger<PeerWireClient>();
                var pwc = new PeerWireClient(conn, pwcLogger);
                peerConnections.TryAdd(conn.PeerId.Value, pwc);
                _swarmTasks[infoHash].TryAdd(conn.PeerId.Value, torrentRunner.InitiatePeer(metaData, pwc, stoppingToken));
                await bus.Publish(new PeerConnectedEvent
                {
                    InfoHash = infoHash,
                    Ip = peerInfo.Ip.ToString(),
                    Port = peerInfo.Port,
                    PeerId = conn.PeerId.Value
                });
            }

            return result.Success;
        }

        public async Task<ICollection<PeerWireClient>> GetPeers(InfoHash infoHash)
        {
            if (!_peerSwarms.TryGetValue(infoHash, out var peerConnections))
                return Array.Empty<PeerWireClient>();

            return peerConnections.Values.ToArray();
        }


        //TODO - Figure this out once at startup and then maintain a list of valid trackers
        private async IAsyncEnumerable<AnnounceResponse> Announce(TorrentMetadata meta)
        {
            //Get my verified bitfield
            if (!bitfields.TryGetVerificationBitfield(meta.InfoHash, out var bitfield))
            {
                logger.LogError("Failed to get verified bitfield for {InfoHash}", meta.InfoHash);
                yield break;
            }

            //Determine bytes remaining to download
            var totalBytes = meta.PieceSize * meta.NumberOfPieces;
            var downloadedBytesF = bitfield.CompletionRatio * totalBytes;
            var downloadedBytes = (long)downloadedBytesF;
            var remainingBytes = totalBytes - downloadedBytes;

            foreach (var tracker in trackerClients)
            {
                if (!tracker.IsValidAnnounceForClient(meta.AnnounceList.First())) continue;
                var announceResponse = await tracker.Announce(new AnnounceRequest
                {
                    InfoHash = meta.InfoHash,
                    PeerId = peerService.Self.Id,
                    Url = meta.AnnounceList.First(),
                    NumWant = 50,
                    BytesUploaded = 0,
                    BytesDownloaded = downloadedBytes,
                    BytesRemaining = remainingBytes,
                    Port = 53123 //TODO - lift this to settings
                });

                if (announceResponse == null) continue;
                yield return announceResponse;
            }
        }
    }

    public class PeerSwarmConfiguration
    {
        public required InfoHash InfoHash { get; init; }
        public required int SwarmSize { get; set; }
    }
}
