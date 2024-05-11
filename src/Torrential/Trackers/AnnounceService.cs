using MassTransit;
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
        PeerSwarm peerSwarm,
        BitfieldManager bitfields,
        IPeerService peerService,
        IEnumerable<ITrackerClient> trackerClients,
        SettingsManager settingsManager,
        HandshakeService handshakeService,
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
                    await foreach (var announceResponse in Announce(torrent))
                    {
                        foreach (var peer in announceResponse.Peers)
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                                    var endpoint = new IPEndPoint(peer.Ip, peer.Port);
                                    await socket.ConnectAsync(endpoint, timeoutToken.Token);

                                    var conn = new PeerWireSocketConnection(socket, handshakeService, logger);
                                    var result = await conn.ConnectOutbound(torrent.InfoHash, peer, stoppingToken);

                                    if (!result.Success)
                                    {
                                        conn.Dispose();
                                        return;
                                    }

                                    await peerSwarm.AddToSwarm(conn);
                                }
                                catch
                                {
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


    public sealed class AnnounceServiceEventHandler(AnnounceServiceState state, TorrentMetadataCache metaCache, ILogger<AnnounceServiceEventHandler> logger) :
        IConsumer<TorrentStartedEvent>,
        IConsumer<TorrentStoppedEvent>
    {
        public Task Consume(ConsumeContext<TorrentStartedEvent> context)
        {
            var @event = context.Message;
            if (!metaCache.TryGet(@event.InfoHash, out var metaData))
            {
                logger.LogInformation("Could not find metadata for {InfoHash}", @event.InfoHash);
                return Task.CompletedTask;
            }

            state.AddTorrent(metaData);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<TorrentStoppedEvent> context)
        {
            var @event = context.Message;
            state.RemoveTorrent(@event.InfoHash);
            return Task.CompletedTask;
        }
    }

}
