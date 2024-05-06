using Microsoft.AspNetCore.SignalR;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR
{
    public interface ITorrentClient
    {
        Task TorrentAdded(TorrentAddedEvent @event);
        Task TorrentRemoved(TorrentRemovedEvent @event);
        Task TorrentCompleted(TorrentCompleteEvent @event);
        Task TorrentStarted(TorrentStartedEvent @event);

        Task TorrentStopped(TorrentStoppedEvent @event);

        Task PieceDownloaded(TorrentPieceDownloadedEvent @event);
        Task PieceVerified(TorrentPieceVerifiedEvent @event);

        Task PeerConnected(PeerConnectedEvent @event);
        Task PeerDisconnected(PeerDisconnectedEvent @event);

        Task PeerBitfieldReceived(PeerBitfieldReceivedEvent @event);
    }



    public sealed class TorrentHub : Hub<ITorrentClient>
    {
        public async Task TorrentAdded(TorrentAddedEvent @event)
            => await Clients.All.TorrentAdded(@event);

        public async Task TorrentRemoved(TorrentRemovedEvent @event)
            => await Clients.All.TorrentRemoved(@event);

        public async Task TorrentCompleted(TorrentCompleteEvent @event)
            => await Clients.All.TorrentCompleted(@event);

        public async Task TorrentStarted(TorrentStartedEvent @event)
            => await Clients.All.TorrentStarted(@event);

        public async Task TorrentStopped(TorrentStoppedEvent @event)
            => await Clients.All.TorrentStopped(@event);

        public async Task PieceDownloaded(TorrentPieceDownloadedEvent @event)
            => await Clients.All.PieceDownloaded(@event);

        public async Task PieceVerified(TorrentPieceVerifiedEvent @event)
            => await Clients.All.PieceVerified(@event);

        public async Task PeerConnected(PeerConnectedEvent @event)
            => await Clients.All.PeerConnected(@event);

        public async Task PeerDisconnected(PeerDisconnectedEvent @event)
            => await Clients.All.PeerDisconnected(@event);

        public async Task PeerBitfieldReceived(PeerBitfieldReceivedEvent @event)
            => await Clients.All.PeerBitfieldReceived(@event);
    }
}

