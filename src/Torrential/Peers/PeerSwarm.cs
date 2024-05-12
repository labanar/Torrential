using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Torrential.Files;
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
        TorrentStatusCache statusCache,
        BitfieldManager bitfieldManager,
        IFileSegmentSaveService fileSegmentSaveService,
        ILoggerFactory loggerFactory)
    {


        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> _peerSwarms = [];
        private ConcurrentDictionary<InfoHash, CancellationTokenSource> _torrentCts = [];
        private ConcurrentDictionary<PeerId, CancellationTokenSource> _peerCts = [];
        private ConcurrentDictionary<PeerId, Task> _peerMessageProcessTasks = [];
        private ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerId, Task>> _peerUploadDownloadTasks = [];
        public IReadOnlyDictionary<InfoHash, ConcurrentDictionary<PeerId, PeerWireClient>> PeerClients => _peerSwarms;

        public async Task MaintainSwarm(InfoHash infoHash, CancellationToken stoppingToken)
        {
            if (!metadataCache.TryGet(infoHash, out var metadata))
            {
                logger.LogError("Metadata not found for {InfoHash}", infoHash);
                return;
            }

            _torrentCts.TryAdd(infoHash, CancellationTokenSource.CreateLinkedTokenSource(stoppingToken));

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupPeers(stoppingToken);
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

            if (await statusCache.GetStatus(connection.InfoHash) != TorrentStatus.Running)
            {
                logger.LogInformation("Torrent {InfoHash} is not running, not adding peer", connection.InfoHash);
                await connection.DisposeAsync();
                return;
            }

            if (peerConnections.Count >= torrentSettings.MaxConnections)
            {
                logger.LogInformation("Peer limit reached for {InfoHash}", metadata.InfoHash);
                await connection.DisposeAsync();
                return;
            }

            if (_peerSwarms.Values.Sum(x => x.Count) >= globalSettings.MaxConnections)
            {
                logger.LogInformation("Global peer limit reached");
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
                logger.LogInformation("Failed to retrieve verification bitfield");
                await connection.DisposeAsync();
                return;
            }

            if (!_torrentCts.TryGetValue(metadata.InfoHash, out var torrentCts))
            {
                logger.LogInformation("Torrent not found in swarm");
                await connection.DisposeAsync();
                return;
            }

            var peerClientLogger = loggerFactory.CreateLogger<PeerWireClient>();
            var peerClient = new PeerWireClient(connection, peerClientLogger);
            var cts = _peerCts.GetOrAdd(connection.PeerId.Value, (_) => CancellationTokenSource.CreateLinkedTokenSource(torrentCts.Token));
            var processTask = peerClient.Process(metadata, bitfieldManager, fileSegmentSaveService, cts.Token);
            _peerMessageProcessTasks.TryAdd(peerClient.PeerId, processTask);


            logger.LogInformation("Sending our bitfield to peer");
            await peerClient.SendBitfield(verificationBitfield);
            logger.LogInformation("Waiting for peer to send bitfield");
            while (peerClient.State.PeerBitfield == null && !cts.Token.IsCancellationRequested)
                await Task.Delay(100);


            if (peerClient.State.PeerBitfield == null)
            {
                logger.LogInformation("Peer did not send bitfield");
                _peerMessageProcessTasks.TryRemove(peerClient.PeerId, out _);
                _peerCts.TryRemove(peerClient.PeerId, out _);


                cts.Cancel();
                await peerClient.DisposeAsync();
                return;
            }

            logger.LogInformation("Peer bitfield received");

            if (verificationBitfield.HasAll() && peerClient.State.PeerBitfield.HasAll())
            {
                logger.LogInformation("Both self and peer are seeds, denying entry to swarm");
                _peerMessageProcessTasks.TryRemove(peerClient.PeerId, out _);
                _peerCts.TryRemove(peerClient.PeerId, out _);
                await peerClient.DisposeAsync();
                return;
            }

            peerConnections.TryAdd(connection.PeerId.Value, peerClient);

            var peerSwarmTasks = _peerUploadDownloadTasks.GetOrAdd(connection.InfoHash, (_) => new ConcurrentDictionary<PeerId, Task>());
            peerSwarmTasks.TryAdd(connection.PeerId.Value, torrentRunner.StartSharing(metadata, peerClient, CancellationToken.None));
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
            var conn = await ConnectToPeer(metaData.InfoHash, peerInfo, stoppingToken);
            if (conn == null) return true;
            await AddToSwarm(conn);
            return true;
        }

        private async Task<PeerWireConnection?> ConnectToPeer(InfoHash infoHash, PeerInfo peerInfo, CancellationToken stoppingToken)
        {
            var conn = new PeerWireConnection(handshakeService, new TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
            var result = await conn.ConnectOutbound(infoHash, peerInfo, stoppingToken);
            if (result.Success && conn.PeerId != null)
            {
                return conn;
            }

            await conn.DisposeAsync();
            return null;
        }

        private async Task CleanupPeers(CancellationToken stoppingToken)
        {
            foreach (var (infoHash, peerClients) in _peerSwarms)
            {
                foreach (var (peerId, peerClient) in peerClients)
                {
                    //Has the peer sent us messages in the last 2 minutes?
                    if (peerClient.LastMessageTimestamp < DateTimeOffset.UtcNow.AddMinutes(-2))
                    {
                        logger.LogInformation("Peer {PeerId} has not sent messages in the last 2 minutes", peerId.ToAsciiString());
                        await CleanupPeer(infoHash, peerId);
                    }

                    //Is the peer's upload/download task still running?
                    if (_peerUploadDownloadTasks.TryGetValue(infoHash, out var uploadDownloadTasks))
                    {
                        if (uploadDownloadTasks.TryGetValue(peerId, out var task))
                        {
                            if (task.IsCompleted || task.IsCanceled || task.IsFaulted)
                            {
                                logger.LogInformation("Peer {PeerId} upload/download task is complete", peerId.ToAsciiString());
                                await CleanupPeer(infoHash, peerId);
                            }
                        }
                    }
                }
            }
        }
        private async Task CleanupPeer(InfoHash infoHash, PeerId peerId)
        {
            //Get this peer's cancellation token source
            if (_peerCts.TryGetValue(peerId, out var cts))
            {
                logger.LogInformation("Cancelling peer {PeerId} task", peerId.ToAsciiString());
                cts.Cancel();
                _peerCts.TryRemove(peerId, out _);
            }

            //Remove the peer message processing task
            if (_peerMessageProcessTasks.TryGetValue(peerId, out var processTask))
            {
                logger.LogInformation("Removing peer message processing task");
                _peerMessageProcessTasks.TryRemove(peerId, out _);
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
            if (_peerSwarms.TryGetValue(infoHash, out var peerClients))
            {
                if (peerClients.TryRemove(peerId, out var peerClient))
                {
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
