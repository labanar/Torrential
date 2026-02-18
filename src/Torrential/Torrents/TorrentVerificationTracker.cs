using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Torrential.Torrents;

/// <summary>
/// Tracks in-flight startup verification requests so UI state can represent
/// "Verifying" until all queued validation work for a torrent has completed.
/// </summary>
public sealed class TorrentVerificationTracker(TorrentEventBus eventBus, ILogger<TorrentVerificationTracker> logger)
{
    private readonly ConcurrentDictionary<InfoHash, int> _pendingByTorrent = [];

    public async Task BeginTracking(InfoHash infoHash, int queuedCount)
    {
        if (queuedCount <= 0)
            return;

        _pendingByTorrent.AddOrUpdate(infoHash, queuedCount, (_, _) => queuedCount);
        logger.LogInformation("Starting verification tracking for {Torrent}: {QueuedCount} queued pieces", infoHash, queuedCount);
        await eventBus.PublishTorrentVerificationStarted(new TorrentVerificationStartedEvent { InfoHash = infoHash });
    }

    public async Task MarkValidationCompleted(InfoHash infoHash)
    {
        if (!_pendingByTorrent.TryGetValue(infoHash, out _))
            return;

        var remaining = _pendingByTorrent.AddOrUpdate(infoHash, 0, (_, current) => Math.Max(0, current - 1));
        if (remaining != 0)
            return;

        if (_pendingByTorrent.TryRemove(infoHash, out _))
        {
            logger.LogInformation("Verification tracking completed for {Torrent}", infoHash);
            await eventBus.PublishTorrentVerificationCompleted(new TorrentVerificationCompletedEvent { InfoHash = infoHash });
        }
    }

    public void Clear(InfoHash infoHash)
    {
        _pendingByTorrent.TryRemove(infoHash, out _);
    }

    public Task HandleTorrentRemoved(TorrentRemovedEvent evt)
    {
        Clear(evt.InfoHash);
        return Task.CompletedTask;
    }
}
