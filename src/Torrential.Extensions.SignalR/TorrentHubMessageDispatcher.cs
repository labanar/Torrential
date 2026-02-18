using Microsoft.AspNetCore.SignalR;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR
{
    /// <summary>
    /// Dispatches torrent events to SignalR clients.
    /// Each handler is registered on TorrentEventBus during DI setup.
    /// No MassTransit dependency -- zero ConsumeContext allocations.
    /// </summary>
    public sealed class TorrentHubMessageDispatcher(
        IHubContext<TorrentHub, ITorrentClient> hubContext,
        PieceVerifiedBatchService pieceVerifiedBatch)
    {
        public async Task HandleTorrentAdded(TorrentAddedEvent evt) =>
            await hubContext.Clients.All.TorrentAdded(evt);

        public async Task HandleTorrentStarted(TorrentStartedEvent evt) =>
            await hubContext.Clients.All.TorrentStarted(evt);

        public async Task HandleTorrentStopped(TorrentStoppedEvent evt) =>
            await hubContext.Clients.All.TorrentStopped(evt);

        public async Task HandleTorrentComplete(TorrentCompleteEvent evt) =>
            await hubContext.Clients.All.TorrentCompleted(evt);

        public async Task HandleTorrentVerificationStarted(TorrentVerificationStartedEvent evt) =>
            await hubContext.Clients.All.TorrentVerificationStarted(evt);

        public async Task HandleTorrentVerificationCompleted(TorrentVerificationCompletedEvent evt) =>
            await hubContext.Clients.All.TorrentVerificationCompleted(evt);

        public async Task HandleTorrentFileCopyStarted(TorrentFileCopyStartedEvent evt) =>
            await hubContext.Clients.All.TorrentFileCopyStarted(evt);

        public async Task HandleTorrentFileCopyCompleted(TorrentFileCopyCompletedEvent evt) =>
            await hubContext.Clients.All.TorrentFileCopyCompleted(evt);

        public async Task HandleTorrentRemoved(TorrentRemovedEvent evt) =>
            await hubContext.Clients.All.TorrentRemoved(evt);

        /// <summary>
        /// Instead of forwarding every piece verification to SignalR immediately,
        /// record the latest progress. The PieceVerifiedBatchService flushes to
        /// SignalR every 250ms -- collapsing hundreds of events into ~4/sec.
        /// Zero allocation: just a dictionary write + queue enqueue per event.
        /// </summary>
        public Task HandlePieceVerified(TorrentPieceVerifiedEvent evt)
        {
            pieceVerifiedBatch.RecordProgress(evt.InfoHash, evt.Progress, evt.PieceIndex);
            return Task.CompletedTask;
        }

        public async Task HandlePeerConnected(PeerConnectedEvent evt) =>
            await hubContext.Clients.All.PeerConnected(evt);

        public async Task HandlePeerDisconnected(PeerDisconnectedEvent evt) =>
            await hubContext.Clients.All.PeerDisconnected(evt);

        public async Task HandlePeerBitfieldReceived(PeerBitfieldReceivedEvent evt) =>
            await hubContext.Clients.All.PeerBitfieldReceived(evt);

        public async Task HandleTorrentStats(TorrentStatsEvent evt) =>
            await hubContext.Clients.All.TorrentStatsUpdated(evt);

        public async Task HandleFileSelectionChanged(FileSelectionChangedEvent evt) =>
            await hubContext.Clients.All.FileSelectionChanged(evt);
    }
}
