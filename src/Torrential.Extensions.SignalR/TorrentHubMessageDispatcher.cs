using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR
{
    public sealed class TorrentHubMessageDispatcher(IHubContext<TorrentHub, ITorrentClient> hubContext)
        : IConsumer<TorrentAddedEvent>,
        IConsumer<TorrentStartedEvent>,
        IConsumer<TorrentStoppedEvent>,
        IConsumer<TorrentCompleteEvent>,
        IConsumer<TorrentRemovedEvent>,
        //IConsumer<TorrentPieceDownloadedEvent>,
        IConsumer<TorrentPieceVerifiedEvent>,
        IConsumer<PeerConnectedEvent>,
        IConsumer<PeerDisconnectedEvent>,
        IConsumer<PeerBitfieldReceivedEvent>,
        IConsumer<TorrentStatsEvent>
    {
        public async Task Consume(ConsumeContext<TorrentAddedEvent> context) =>
            await hubContext.Clients.All.TorrentAdded(context.Message);

        public async Task Consume(ConsumeContext<TorrentStartedEvent> context) =>
            await hubContext.Clients.All.TorrentStarted(context.Message);

        public async Task Consume(ConsumeContext<TorrentStoppedEvent> context) =>
            await hubContext.Clients.All.TorrentStopped(context.Message);

        public async Task Consume(ConsumeContext<TorrentCompleteEvent> context) =>
            await hubContext.Clients.All.TorrentCompleted(context.Message);

        public async Task Consume(ConsumeContext<TorrentRemovedEvent> context) =>
            await hubContext.Clients.All.TorrentRemoved(context.Message);

        //public async Task Consume(ConsumeContext<TorrentPieceDownloadedEvent> context) =>
        //    await hubContext.Clients.All.PieceDownloaded(context.Message);

        public async Task Consume(ConsumeContext<TorrentPieceVerifiedEvent> context) =>
            await hubContext.Clients.All.PieceVerified(context.Message);

        public async Task Consume(ConsumeContext<PeerConnectedEvent> context) =>
            await hubContext.Clients.All.PeerConnected(context.Message);

        public async Task Consume(ConsumeContext<PeerDisconnectedEvent> context) =>
            await hubContext.Clients.All.PeerDisconnected(context.Message);

        public async Task Consume(ConsumeContext<PeerBitfieldReceivedEvent> context) =>
            await hubContext.Clients.All.PeerBitfieldReceived(context.Message);
        public async Task Consume(ConsumeContext<TorrentStatsEvent> context) =>
            await hubContext.Clients.All.TorrentStatsUpdated(context.Message);
    }
}
