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
            return Task.CompletedTask;
        }
    }
}
