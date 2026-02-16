using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;
using Torrential.Settings;
using Torrential.Torrents;
using Torrential.Utilities;

namespace Torrential.Peers
{

    public sealed class PeerSwarm(
        TorrentMetadataCache metadataCache,
        TorrentRunner torrentRunner,
        SettingsManager settingsManager,
        TorrentStatusCache statusCache,
        BitfieldManager bitfieldManager,
        IBus bus,
        IBlockSaveService blockSaveService,
        ILogger<PeerSwarm> logger,
        ILoggerFactory loggerFactory)
    {
        private ConcurrentDictionary<InfoHash, CancellationTokenSource> _torrentCts = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, CancellationTokenSource>> _peerCts = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> _peerClients = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> _peerProcessTasks = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> _peerUploadDownloadTasks = [];


        public async IAsyncEnumerable<InfoHash> TrackedTorrents()
        {
            foreach (var infoHash in _torrentCts.Keys)
                yield return infoHash;
        }

        public async Task MaintainSwarm(InfoHash infoHash)
        {
            if (!metadataCache.TryGet(infoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", infoHash);
                return;
            }

            var cts = new CancellationTokenSource();
            var stoppingToken = cts.Token;
            _torrentCts[infoHash] = cts;

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupPeers();
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }

        public async Task BroadcastHaveMessage(InfoHash infoHash, int pieceIndex)
        {
            if (!_peerClients.TryGetValue(infoHash, out var peerClients))
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
                logger.LogDebug("Broadcasted have message for {PieceIndex} to {Count} peers", pieceIndex, tasks.Count);
            }
        }

        public async Task RemoveSwarm(InfoHash infoHash)
        {
            await CleanupPeers(infoHash);
            _torrentCts.TryRemove(infoHash, out _);
            _peerClients.TryRemove(infoHash, out _);
            _peerProcessTasks.TryRemove(infoHash, out _);
            _peerUploadDownloadTasks.TryRemove(infoHash, out _);
            _peerCts.TryRemove(infoHash, out _);
        }

        public async Task AddToSwarm(IPeerWireConnection connection)
        {
            if (!metadataCache.TryGet(connection.InfoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", connection.InfoHash);
                return;
            }

            var connectionSettings = await settingsManager.GetConnectionSettings();
            var peerConnections = _peerClients.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, PeerWireClient>());

            if (await statusCache.GetStatus(connection.InfoHash) != TorrentStatus.Running)
            {
                logger.LogWarning("Torrent {InfoHash} is not running, not adding peer", connection.InfoHash);
                await connection.DisposeAsync();
                return;
            }

            if (peerConnections.Count >= connectionSettings.MaxConnectionsPerTorrent)
            {
                logger.LogDebug("Peer limit reached for {InfoHash}", metadata.InfoHash);
                await connection.DisposeAsync();
                return;
            }

            if (_peerClients.Values.Sum(x => x.Count) >= connectionSettings.MaxConnectionsGlobal)
            {
                logger.LogDebug("Global peer limit reached");
                await connection.DisposeAsync();
                return;
            }

            if (connection.PeerId == null)
            {
                logger.LogError("PeerId not set for connection {Connection}", connection);
                await connection.DisposeAsync();
                return;
            }

            if (peerConnections.ContainsKey(connection.PeerId.Value))
            {
                logger.LogError("Duplicate peer connection {PeerId}", connection.PeerId.Value);
                await connection.DisposeAsync();
                return;
            };

            if (!bitfieldManager.TryGetVerificationBitfield(metadata.InfoHash, out var verificationBitfield))
            {
                logger.LogWarning("Failed to retrieve verification bitfield");
                await connection.DisposeAsync();
                return;
            }

            if (!_torrentCts.TryGetValue(metadata.InfoHash, out var torrentCts))
            {
                logger.LogDebug("Torrent not found in swarm");
                await connection.DisposeAsync();
                return;
            }


            var torrentPeerCts = _peerCts.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, CancellationTokenSource>());
            var peerCts = torrentPeerCts.GetOrAdd(connection.PeerId.Value, (_) => CancellationTokenSource.CreateLinkedTokenSource(torrentCts.Token));
            var peerClientLogger = loggerFactory.CreateLogger<PeerWireClient>();
            var peerClient = new PeerWireClient(connection, metadataCache, blockSaveService, peerClientLogger, peerCts.Token);


            var processTasks = _peerProcessTasks.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, Task>());
            processTasks.TryAdd(connection.PeerId.Value, peerClient.ProcessMessages());

            logger.LogDebug("Sending our bitfield to peer");
            await peerClient.SendBitfield(verificationBitfield);
            logger.LogDebug("Waiting for peer to send bitfield");


            var bitfieldTimeout = TimeSpan.FromSeconds(10);
            var started = DateTimeOffset.UtcNow;
            while (peerClient.State.PeerBitfield == null && DateTimeOffset.UtcNow.Subtract(started) < bitfieldTimeout)
                await Task.Delay(500);

            if (peerClient.State.PeerBitfield == null)
            {
                logger.LogDebug("Peer did not send bitfield");

                processTasks.TryRemove(connection.PeerId.Value, out _);
                await peerClient.DisposeAsync();
                return;
            }

            logger.LogDebug("Peer bitfield received");

            // Update piece availability counts from the peer's full bitfield
            if (bitfieldManager.TryGetPieceAvailability(connection.InfoHash, out var availability) && availability != null)
            {
                availability.IncrementFromBitfield(
                    peerClient.State.PeerBitfield.Bytes,
                    peerClient.State.PeerBitfield.NumberOfPieces);

                // Wire up the Have callback so individual Have messages also update availability
                peerClient.OnPeerHave = availability.IncrementPiece;
            }

            if (verificationBitfield.HasAll() && peerClient.State.PeerBitfield.HasAll())
            {
                logger.LogDebug("Both self and peer are seeds, denying entry to swarm");

                // Undo the availability increment since this peer is being rejected
                if (availability != null)
                {
                    availability.DecrementFromBitfield(
                        peerClient.State.PeerBitfield.Bytes,
                        peerClient.State.PeerBitfield.NumberOfPieces);
                    peerClient.OnPeerHave = null;
                }

                processTasks.TryRemove(connection.PeerId.Value, out _);
                await peerClient.DisposeAsync();
                return;
            }

            peerConnections.TryAdd(connection.PeerId.Value, peerClient);

            var peerSwarmTasks = _peerUploadDownloadTasks.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, Task>());
            peerSwarmTasks.TryAdd(connection.PeerId.Value, torrentRunner.StartSharing(metadata, peerClient, peerCts.Token));
            await bus.Publish(new PeerConnectedEvent
            {
                InfoHash = metadata.InfoHash,
                Ip = connection.PeerInfo.Ip.ToString(),
                Port = connection.PeerInfo.Port,
                PeerId = connection.PeerId.Value
            });
        }

        private async Task CleanupPeers()
        {
            foreach (var (infoHash, peerClients) in _peerClients)
            {
                foreach (var (peerId, peerClient) in peerClients)
                {
                    //Has the peer sent us messages in the last 2 minutes?
                    var diff = Math.Abs(DateTimeOffset.UtcNow.Subtract(peerClient.LastMessageTimestamp).TotalMinutes);
                    if (diff >= 5)
                    {
                        logger.LogInformation("Peer {PeerId} has not sent messages in the last 5 minutes", peerId.ToAsciiString());
                        await CleanupPeer(infoHash, peerId);
                        continue;
                    }

                    //Is the peer's upload/download task still running?
                    if (_peerUploadDownloadTasks.TryGetValue(infoHash, out var uploadDownloadTasks))
                    {
                        if (uploadDownloadTasks.TryGetValue(peerId, out var task))
                        {
                            if (!task.InProgress())
                            {
                                logger.LogInformation("Peer {PeerId} upload/download task is complete", peerId.ToAsciiString());
                                await CleanupPeer(infoHash, peerId);
                                continue;
                            }
                        }
                    }

                    //Is the peer's process task still running?
                    if (_peerProcessTasks.TryGetValue(infoHash, out var processTasks))
                    {
                        if (processTasks.TryGetValue(peerId, out var task))
                        {
                            if (!task.InProgress())
                            {
                                logger.LogInformation("Peer {PeerId} process task is complete", peerId.ToAsciiString());
                                await CleanupPeer(infoHash, peerId);
                                continue;
                            }
                        }
                    }
                }
            }
        }


        public async Task CleanupPeers(InfoHash infoHash)
        {
            if (_torrentCts.TryRemove(infoHash, out var cts))
                cts.Cancel();

            var cleanupTasks = new List<Task>();
            if (_peerClients.TryGetValue(infoHash, out var peerClients))
            {
                foreach (var (peerId, _) in peerClients)
                {
                    cleanupTasks.Add(CleanupPeer(infoHash, peerId));
                }
            }

            await Task.WhenAll(cleanupTasks);

            _peerClients.TryRemove(infoHash, out _);
        }

        private async Task CleanupPeer(InfoHash infoHash, PeerId peerId)
        {
            //Get the peer cts
            if (_peerCts.TryGetValue(infoHash, out var peerCts))
            {
                if (peerCts.TryRemove(peerId, out var cts))
                {
                    logger.LogInformation("Cancelling peer cts");
                    cts.Cancel();
                }
            }

            //Remove the process task
            if (_peerProcessTasks.TryGetValue(infoHash, out var processTasks))
            {
                if (processTasks.TryRemove(peerId, out _))
                {
                    logger.LogInformation("Removing peer process task");
                }
            }


            //Remove the upload/download task
            if (_peerUploadDownloadTasks.TryGetValue(infoHash, out var uploadDownloadTasks))
            {
                if (uploadDownloadTasks.TryRemove(peerId, out _))
                {
                    logger.LogInformation("Removing peer upload/download task");
                }
            }

            //Remove and dispose of the client
            if (_peerClients.TryGetValue(infoHash, out var peerClients))
            {
                if (peerClients.TryRemove(peerId, out var peerClient))
                {
                    // Decrement piece availability for all pieces this peer had
                    if (peerClient.State.PeerBitfield != null
                        && bitfieldManager.TryGetPieceAvailability(infoHash, out var peerAvailability)
                        && peerAvailability != null)
                    {
                        peerAvailability.DecrementFromBitfield(
                            peerClient.State.PeerBitfield.Bytes,
                            peerClient.State.PeerBitfield.NumberOfPieces);
                        peerClient.OnPeerHave = null;
                    }

                    logger.LogInformation("Disposing peer client");
                    await peerClient.DisposeAsync();
                }
            }

            //Notify that the peer has been removed
            await bus.Publish(new PeerDisconnectedEvent
            {
                InfoHash = infoHash,
                PeerId = peerId
            });
        }

        public async Task<ICollection<PeerWireClient>> GetPeers(InfoHash infoHash)
        {
            if (!_peerClients.TryGetValue(infoHash, out var peerConnections))
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
