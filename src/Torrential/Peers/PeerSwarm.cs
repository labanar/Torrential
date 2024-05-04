using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Torrents;
using Torrential.Trackers;

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
    public sealed class PeerSwarm(
        TorrentMetadataCache metadataCache,
        TorrentRunner torrentRunner,
        IPeerService peerService,
        IEnumerable<ITrackerClient> trackerClients,
        ILogger<PeerSwarm> logger,
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
                await AnnounceAndFillSwarm(metadata, stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
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

            var conn = new PeerWireConnection(peerService, new System.Net.Sockets.TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
            var result = await conn.Connect(infoHash, peerInfo, timedCts.Token);

            if (result.Success && conn.PeerId != null)
            {
                var peerConnections = _peerSwarms.GetOrAdd(infoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());
                if (peerConnections.ContainsKey(conn.PeerId.Value)) return true;
                var pwcLogger = loggerFactory.CreateLogger<PeerWireClient>();
                var pwc = new PeerWireClient(conn, pwcLogger);
                peerConnections.TryAdd(conn.PeerId.Value, pwc);
                _swarmTasks[infoHash].TryAdd(conn.PeerId.Value, torrentRunner.InitiatePeer(metaData, pwc, stoppingToken));
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
                    BytesRemaining = remainingBytes
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
