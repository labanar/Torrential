using System.Collections.Concurrent;

namespace Torrential.Torrents
{
    public class TorrentStatusCache
    {
        private readonly ConcurrentDictionary<string, TorrentStatus> _statuses = new();
        public ValueTask<TorrentStatus> GetStatus(InfoHash infoHash)
        {
            if (!_statuses.TryGetValue(infoHash, out var status))
                return new ValueTask<TorrentStatus>(TorrentStatus.Idle);

            return new ValueTask<TorrentStatus>(status);
        }

        public void UpdateStatus(InfoHash infoHash, TorrentStatus status)
        {
            _statuses.AddOrUpdate(infoHash, status, (_, _) => status);
        }

        public void RemoveStatus(InfoHash infoHash)
        {
            _statuses.TryRemove(infoHash, out _);
        }
    }

    /// <summary>
    /// Handles torrent lifecycle events to keep the status cache in sync.
    /// Registered as direct handlers on TorrentEventBus during DI setup.
    /// </summary>
    public sealed class TorrentStatusCacheMaintainer(TorrentStatusCache torrentStatus)
    {
        private readonly ConcurrentDictionary<InfoHash, int> _copyFileCounters = [];
        private readonly ConcurrentDictionary<InfoHash, TorrentStatus> _preCopyStatuses = [];

        public Task HandleTorrentStarted(TorrentStartedEvent evt)
        {
            torrentStatus.UpdateStatus(evt.InfoHash, TorrentStatus.Running);
            return Task.CompletedTask;
        }

        public Task HandleTorrentStopped(TorrentStoppedEvent evt)
        {
            torrentStatus.UpdateStatus(evt.InfoHash, TorrentStatus.Stopped);
            return Task.CompletedTask;
        }

        public Task HandleTorrentRemoved(TorrentRemovedEvent evt)
        {
            torrentStatus.RemoveStatus(evt.InfoHash);
            _copyFileCounters.TryRemove(evt.InfoHash, out _);
            _preCopyStatuses.TryRemove(evt.InfoHash, out _);
            return Task.CompletedTask;
        }

        public Task HandleVerificationStarted(TorrentVerificationStartedEvent evt)
        {
            torrentStatus.UpdateStatus(evt.InfoHash, TorrentStatus.Verifying);
            return Task.CompletedTask;
        }

        public async Task HandleVerificationCompleted(TorrentVerificationCompletedEvent evt)
        {
            if (await torrentStatus.GetStatus(evt.InfoHash) == TorrentStatus.Verifying)
                torrentStatus.UpdateStatus(evt.InfoHash, TorrentStatus.Idle);
        }

        public async Task HandleFileCopyStarted(TorrentFileCopyStartedEvent evt)
        {
            var inFlightFileCopies = _copyFileCounters.AddOrUpdate(evt.InfoHash, 1, (_, current) => current + 1);
            if (inFlightFileCopies != 1)
                return;

            var priorStatus = await torrentStatus.GetStatus(evt.InfoHash);
            _preCopyStatuses[evt.InfoHash] = priorStatus == TorrentStatus.Copying ? TorrentStatus.Idle : priorStatus;
            torrentStatus.UpdateStatus(evt.InfoHash, TorrentStatus.Copying);
        }

        public async Task HandleFileCopyCompleted(TorrentFileCopyCompletedEvent evt)
        {
            if (!_copyFileCounters.TryGetValue(evt.InfoHash, out _))
                return;

            var remaining = _copyFileCounters.AddOrUpdate(evt.InfoHash, 0, (_, current) => Math.Max(0, current - 1));
            if (remaining != 0)
                return;

            _copyFileCounters.TryRemove(evt.InfoHash, out _);
            var restoreStatus = _preCopyStatuses.TryRemove(evt.InfoHash, out var previous)
                ? previous
                : TorrentStatus.Idle;

            if (await torrentStatus.GetStatus(evt.InfoHash) == TorrentStatus.Copying)
                torrentStatus.UpdateStatus(evt.InfoHash, restoreStatus);
        }
    }
}
