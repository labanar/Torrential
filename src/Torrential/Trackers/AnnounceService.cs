using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Torrential.Peers;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Trackers
{
    internal class AnnounceService(
        AnnounceServiceState state,
        BitfieldManager bitfields,
        IPeerService peerService,
        IEnumerable<ITrackerClient> trackerClients,
        SettingsManager settingsManager,
        PeerConnectionManager connectionManager,
        TorrentStats stats,
        ILogger<AnnounceService> logger)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
            logger.LogInformation("Announce service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                var processedAny = false;
                foreach (var torrent in state.ActiveTorrents)
                {
                    processedAny = true;
                    logger.LogInformation("Announcing {InfoHash}", torrent.InfoHash);
                    await foreach (var announceResponse in Announce(torrent))
                    {
                        foreach (var peer in announceResponse.Peers)
                        {
                            _ = Task.Run(async () =>
                            {
                                var conn = default(PeerWireSocketConnection);
                                var socket = default(Socket);
                                try
                                {
                                    var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                    socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                                    var endpoint = new IPEndPoint(peer.Ip, peer.Port);
                                    await socket.ConnectAsync(endpoint, timeoutToken.Token);
                                    conn = new PeerWireSocketConnection(socket, logger);
                                    await connectionManager.QueueOutboundConnection(conn, torrent.InfoHash);
                                }
                                catch
                                {
                                    if (conn != null)
                                        await conn.DisposeAsync();
                                    else if (socket != null)
                                        socket.Dispose();
                                }
                            });
                        }
                    }
                }

                if (!processedAny)
                {
                    await Task.Delay(1000);
                    continue;
                }

                await timer.WaitForNextTickAsync(stoppingToken);
            }

            logger.LogInformation("Announce service stopped");
        }

        private async IAsyncEnumerable<AnnounceResponse> Announce(TorrentMetadata meta)
        {
            if (!bitfields.TryGetVerificationBitfield(meta.InfoHash, out var bitfield))
            {
                logger.LogError("Failed to get verified bitfield for {InfoHash}", meta.InfoHash);
                yield break;
            }

            var totalBytes = meta.PieceSize * meta.NumberOfPieces;
            var downloadedBytesF = bitfield.CompletionRatio * totalBytes;
            var downloadedBytes = (long)downloadedBytesF;
            var remainingBytes = totalBytes - downloadedBytes;
            var tcpSettings = await settingsManager.GetTcpListenerSettings();
            var bytesUploaded = stats.GetTotalUploaded(meta.InfoHash);

            foreach (var tracker in trackerClients)
            {
                if (!tracker.IsValidAnnounceForClient(meta.AnnounceList.First())) continue;
                var announceResponse = await tracker.Announce(new AnnounceRequest
                {
                    InfoHash = meta.InfoHash,
                    PeerId = peerService.Self.Id,
                    Url = meta.AnnounceList.First(),
                    NumWant = 50,
                    BytesUploaded = bytesUploaded,
                    BytesDownloaded = downloadedBytes,
                    BytesRemaining = remainingBytes,
                    Port = tcpSettings.Port
                });

                if (announceResponse == null) continue;
                yield return announceResponse;
            }
        }
    }

    public sealed class AnnounceServiceState()
    {
        private readonly ConcurrentDictionary<InfoHash, TorrentMetadata> _torrents = new();

        public void AddTorrent(TorrentMetadata metadata)
        {
            _torrents.TryAdd(metadata.InfoHash, metadata);
        }

        public void RemoveTorrent(InfoHash infoHash)
        {
            _torrents.TryRemove(infoHash, out _);
        }

        public IEnumerable<TorrentMetadata> ActiveTorrents => _torrents.Values;
    }


    /// <summary>
    /// Handles torrent lifecycle events for the announce service.
    /// Registered as direct handlers on TorrentEventBus during DI setup.
    /// </summary>
    public sealed class AnnounceServiceEventHandler(AnnounceServiceState state, TorrentMetadataCache metaCache, ILogger<AnnounceServiceEventHandler> logger)
    {
        public Task HandleTorrentStarted(TorrentStartedEvent evt)
        {
            if (!metaCache.TryGet(evt.InfoHash, out var metaData))
            {
                logger.LogInformation("Could not find metadata for {InfoHash}", evt.InfoHash);
                return Task.CompletedTask;
            }

            state.AddTorrent(metaData);
            return Task.CompletedTask;
        }

        public Task HandleTorrentStopped(TorrentStoppedEvent evt)
        {
            state.RemoveTorrent(evt.InfoHash);
            return Task.CompletedTask;
        }

        public Task HandleTorrentRemoved(TorrentRemovedEvent evt)
        {
            state.RemoveTorrent(evt.InfoHash);
            return Task.CompletedTask;
        }
    }

}
