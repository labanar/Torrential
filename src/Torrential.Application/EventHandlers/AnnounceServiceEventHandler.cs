using Microsoft.Extensions.Logging;
using Torrential.Application.Events;
using Torrential.Application.Torrents;
using Torrential.Application.Trackers;

namespace Torrential.Application.EventHandlers;

public sealed class AnnounceServiceEventHandler(AnnounceServiceState state, TorrentMetadataCache metaCache, ILogger<AnnounceServiceEventHandler> logger)
    : IEventHandler<TorrentStartedEvent>,
      IEventHandler<TorrentStoppedEvent>,
      IEventHandler<TorrentRemovedEvent>
{
    public Task HandleAsync(TorrentStartedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!metaCache.TryGet(@event.InfoHash, out var metaData))
        {
            logger.LogInformation("Could not find metadata for {InfoHash}", @event.InfoHash);
            return Task.CompletedTask;
        }

        state.AddTorrent(metaData);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TorrentStoppedEvent @event, CancellationToken cancellationToken = default)
    {
        state.RemoveTorrent(@event.InfoHash);
        return Task.CompletedTask;
    }

    public Task HandleAsync(TorrentRemovedEvent @event, CancellationToken cancellationToken = default)
    {
        state.RemoveTorrent(@event.InfoHash);
        return Task.CompletedTask;
    }
}
