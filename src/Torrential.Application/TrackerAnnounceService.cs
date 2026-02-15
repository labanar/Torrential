using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public sealed class TrackerAnnounceService(
    ITorrentManager torrentManager,
    IAnnounceService announceService,
    IPeerPool peerPool,
    ILogger<TrackerAnnounceService> logger) : BackgroundService
{
    private readonly PeerId _peerId = PeerId.New;
    private readonly ConcurrentDictionary<InfoHash, AnnounceState> _announceStates = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TrackerAnnounceService started with PeerId: {PeerId}", _peerId.ToAsciiString());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AnnounceActiveTorrents(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in TrackerAnnounceService loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task AnnounceActiveTorrents(CancellationToken stoppingToken)
    {
        var activeTorrents = torrentManager.GetTorrentsByStatus(TorrentStatus.Downloading);
        var activeSet = new HashSet<InfoHash>(activeTorrents);

        // Clean up announce state for torrents that are no longer active
        foreach (var infoHash in _announceStates.Keys)
        {
            if (!activeSet.Contains(infoHash))
            {
                _announceStates.TryRemove(infoHash, out _);
            }
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var infoHash in activeTorrents)
        {
            var state = _announceStates.GetOrAdd(infoHash, _ => new AnnounceState());

            if (now - state.LastAnnounce < TimeSpan.FromSeconds(state.IntervalSeconds))
                continue;

            var metaInfo = torrentManager.GetMetaInfo(infoHash);
            if (metaInfo is null)
                continue;

            try
            {
                var announceParams = new AnnounceParams(_peerId, Port: 6881);
                var responses = await announceService.AnnounceAsync(metaInfo, announceParams, stoppingToken);

                state.LastAnnounce = DateTimeOffset.UtcNow;

                var totalPeersDiscovered = 0;
                foreach (var response in responses)
                {
                    if (response.Interval > 0)
                        state.IntervalSeconds = response.Interval;

                    peerPool.AddPeers(infoHash, response.Peers);
                    totalPeersDiscovered += response.Peers.Count;
                }

                if (totalPeersDiscovered > 0)
                {
                    logger.LogInformation(
                        "Announced {Name} ({InfoHash}) — discovered {PeerCount} peers, total pool: {TotalPeers}, next announce in {Interval}s",
                        metaInfo.Name, infoHash.AsString(), totalPeersDiscovered, peerPool.GetPeerCount(infoHash), state.IntervalSeconds);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to announce torrent {InfoHash}", infoHash.AsString());
            }
        }
    }

    private sealed class AnnounceState
    {
        public DateTimeOffset LastAnnounce { get; set; } = DateTimeOffset.MinValue;
        public int IntervalSeconds { get; set; } = 60;
    }
}
