using MassTransit;
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



    public sealed class TorrentStatusCacheMaintainer(TorrentStatusCache torrentStatus)
        : IConsumer<TorrentStartedEvent>,
          IConsumer<TorrentStoppedEvent>,
          IConsumer<TorrentRemovedEvent>
    {
        public Task Consume(ConsumeContext<TorrentStartedEvent> context)
        {
            var @event = context.Message;
            torrentStatus.UpdateStatus(@event.InfoHash, TorrentStatus.Running);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<TorrentStoppedEvent> context)
        {
            var @event = context.Message;
            torrentStatus.UpdateStatus(@event.InfoHash, TorrentStatus.Stopped);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<TorrentRemovedEvent> context)
        {
            var @event = context.Message;
            torrentStatus.RemoveStatus(@event.InfoHash);
            return Task.CompletedTask;
        }
    }
}
