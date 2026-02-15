using Microsoft.AspNetCore.SignalR;
using Torrential.Application.Events;

namespace Torrential.Extensions.SignalR
{
    public sealed class TorrentHubMessageDispatcher(IHubContext<TorrentHub, ITorrentClient> hubContext)
        : IEventHandler<TorrentAddedEvent>,
        IEventHandler<TorrentStartedEvent>,
        IEventHandler<TorrentStoppedEvent>,
        IEventHandler<TorrentCompleteEvent>,
        IEventHandler<TorrentRemovedEvent>,
        IEventHandler<TorrentPieceVerifiedEvent>,
        IEventHandler<PeerConnectedEvent>,
        IEventHandler<PeerDisconnectedEvent>,
        IEventHandler<PeerBitfieldReceivedEvent>,
        IEventHandler<TorrentStatsEvent>
    {
        public async Task HandleAsync(TorrentAddedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentAdded(@event);

        public async Task HandleAsync(TorrentStartedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentStarted(@event);

        public async Task HandleAsync(TorrentStoppedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentStopped(@event);

        public async Task HandleAsync(TorrentCompleteEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentCompleted(@event);

        public async Task HandleAsync(TorrentRemovedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentRemoved(@event);

        public async Task HandleAsync(TorrentPieceVerifiedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.PieceVerified(@event);

        public async Task HandleAsync(PeerConnectedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.PeerConnected(@event);

        public async Task HandleAsync(PeerDisconnectedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.PeerDisconnected(@event);

        public async Task HandleAsync(PeerBitfieldReceivedEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.PeerBitfieldReceived(@event);

        public async Task HandleAsync(TorrentStatsEvent @event, CancellationToken cancellationToken = default) =>
            await hubContext.Clients.All.TorrentStatsUpdated(@event);
    }
}
