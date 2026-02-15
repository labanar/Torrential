using Torrential.Application.Events;
using Torrential.Application.Torrents;

namespace Torrential.Application.EventHandlers;

public class TorrentStatsEventHandler(TorrentStats rates)
    : IEventHandler<TorrentBlockDownloaded>,
      IEventHandler<TorrentBlockUploadedEvent>
{
    public async Task HandleAsync(TorrentBlockDownloaded @event, CancellationToken cancellationToken = default)
    {
        await rates.QueueDownloadRate(@event.InfoHash, @event.Length);
    }

    public async Task HandleAsync(TorrentBlockUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        await rates.QueueUploadRate(@event.InfoHash, @event.Length);
    }
}
