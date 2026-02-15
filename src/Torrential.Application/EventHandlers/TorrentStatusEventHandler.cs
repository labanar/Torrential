using Torrential.Application.Data;
using Torrential.Application.Events;
using Torrential.Application.Torrents;

namespace Torrential.Application.EventHandlers;

public sealed class TorrentStatusEventHandler(TorrentStatusCache torrentStatus)
    : IEventHandler<TorrentStartedEvent>,
      IEventHandler<TorrentStoppedEvent>,
      IEventHandler<TorrentRemovedEvent>
{
    public Task HandleAsync(TorrentStartedEvent @event, CancellationToken cancellationToken = default)
    {
        torrentStatus.UpdateStatus(@event.InfoHash, TorrentStatus.Running);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TorrentStoppedEvent @event, CancellationToken cancellationToken = default)
    {
        torrentStatus.UpdateStatus(@event.InfoHash, TorrentStatus.Stopped);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TorrentRemovedEvent @event, CancellationToken cancellationToken = default)
    {
        torrentStatus.RemoveStatus(@event.InfoHash);
        return Task.CompletedTask;
    }
}
