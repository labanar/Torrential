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
///
/// Additionally tracks verified piece indices per batch window so the detail pane
/// can incrementally update its local bitfield without re-fetching.
/// </summary>
public sealed class PieceVerifiedBatchService : BackgroundService
{
    private readonly IHubContext<TorrentHub, ITorrentClient> _hubContext;

    // Latest progress snapshot per torrent. Written by event bus handler thread,
    // drained by the flush timer thread. ConcurrentDictionary handles the concurrency.
    private readonly ConcurrentDictionary<InfoHash, float> _pendingProgress = new();

    // Accumulates verified piece indices per torrent within the current batch window.
    // ConcurrentQueue is thread-safe for concurrent Enqueue; the flush drains it.
    private readonly ConcurrentDictionary<InfoHash, ConcurrentQueue<int>> _pendingPieceIndices = new();

    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);

    public PieceVerifiedBatchService(IHubContext<TorrentHub, ITorrentClient> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <summary>
    /// Called by the event bus handler to record the latest progress and piece index.
    /// This is a cheap dictionary write + queue enqueue -- no SignalR, no serialization, no async.
    /// </summary>
    public void RecordProgress(InfoHash infoHash, float progress, int pieceIndex)
    {
        _pendingProgress[infoHash] = progress;

        var queue = _pendingPieceIndices.GetOrAdd(infoHash, static _ => new ConcurrentQueue<int>());
        queue.Enqueue(pieceIndex);
    }

    /// <summary>
    /// Overload for callers that don't have a piece index (backward compatibility).
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
                // Drain accumulated piece indices for this batch
                int[] verifiedPieces = Array.Empty<int>();
                if (_pendingPieceIndices.TryRemove(infoHash, out var queue))
                {
                    var list = new List<int>();
                    while (queue.TryDequeue(out var idx))
                        list.Add(idx);
                    if (list.Count > 0)
                        verifiedPieces = list.ToArray();
                }

                await _hubContext.Clients.All.PieceVerified(new TorrentPieceVerifiedEvent
                {
                    InfoHash = infoHash,
                    PieceIndex = -1, // Batched -- use VerifiedPieces for individual indices
                    Progress = progress,
                    VerifiedPieces = verifiedPieces
                });
            }
        }
    }
}
