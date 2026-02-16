using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR;

/// <summary>
/// Accumulates the latest PieceVerified progress per torrent and flushes
/// to SignalR on a fixed interval, collapsing hundreds of per-piece events
/// into at most ~4 sends/sec per active torrent.
///
/// The ConcurrentDictionary key is an InfoHash (readonly record struct, 20 bytes,
/// value-equality by default) so no heap allocation for the key. The value is a
/// float written atomically via TryRemove during flush.
/// </summary>
public sealed class PieceVerifiedBatchService : BackgroundService
{
    private readonly IHubContext<TorrentHub, ITorrentClient> _hubContext;

    // Latest progress snapshot per torrent. Written by event bus handler thread,
    // drained by the flush timer thread. ConcurrentDictionary handles the concurrency.
    private readonly ConcurrentDictionary<InfoHash, float> _pendingProgress = new();

    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);

    public PieceVerifiedBatchService(IHubContext<TorrentHub, ITorrentClient> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Called by the event bus handler to record the latest progress.
    /// This is a cheap dictionary write -- no SignalR, no serialization, no async.
    /// </summary>
    public void RecordProgress(InfoHash infoHash, float progress)
    {
        _pendingProgress[infoHash] = progress;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(stoppingToken);
            await FlushAsync();
        }
    }

    private async Task FlushAsync()
    {
        // Snapshot-and-clear: TryRemove each key so we only send updates
        // for torrents that had progress since the last flush.
        // Iterating Keys then TryRemove is safe with ConcurrentDictionary.
        foreach (var infoHash in _pendingProgress.Keys)
        {
            if (_pendingProgress.TryRemove(infoHash, out var progress))
            {
                await _hubContext.Clients.All.PieceVerified(new TorrentPieceVerifiedEvent
                {
                    InfoHash = infoHash,
                    PieceIndex = -1, // Batched -- individual index is irrelevant to the UI
                    Progress = progress
                });
            }
        }
    }
}
