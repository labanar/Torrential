using Microsoft.Extensions.Logging;
using Torrential.Application.Events;
using Torrential.Application.Peers;

namespace Torrential.Application.EventHandlers;

public sealed class PeerSwarmMessageDispatcher(IPeerSwarm peerSwarm, ILogger<PeerSwarmMessageDispatcher> logger)
    : IEventHandler<TorrentPieceVerifiedEvent>
{
    public async Task HandleAsync(TorrentPieceVerifiedEvent @event, CancellationToken cancellationToken = default)
    {
        await peerSwarm.BroadcastHaveMessage(@event.InfoHash, @event.PieceIndex);
    }
}
