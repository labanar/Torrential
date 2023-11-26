using Microsoft.AspNetCore.SignalR;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR
{
    public interface ITorrentClient
    {
        Task TorrentAdded(TorrentMetadata infoHash);
        Task TorrentRemoved(InfoHash infoHash);
        Task TorrentCompleted(InfoHash infoHash);
        Task TorrentStarted(InfoHash infoHash);

        Task TorrentStopped(InfoHash infoHash);

        Task PieceDownloaded(InfoHash infoHash, int pieceIndex);
        Task PieceVerified(InfoHash infoHash, int pieceIndex);

        Task PeerConnected(InfoHash infoHash);
        Task PeerDisconnected(InfoHash infoHash);
    }



    public sealed class TorrentHub : Hub<ITorrentClient>
    {
        public async Task TorrentAdded(TorrentMetadata metadata)
            => await Clients.All.TorrentAdded(metadata);

        public async Task TorrentRemoved(InfoHash infoHash)
            => await Clients.All.TorrentRemoved(infoHash);

        public async Task TorrentCompleted(InfoHash infoHash)
            => await Clients.All.TorrentCompleted(infoHash);

        public async Task TorrentStarted(InfoHash infoHash)
            => await Clients.All.TorrentStarted(infoHash);

        public async Task TorrentStopped(InfoHash infoHash)
            => await Clients.All.TorrentStopped(infoHash);

        public async Task PieceDownloaded(InfoHash infoHash, int pieceIndex)
            => await Clients.All.PieceDownloaded(infoHash, pieceIndex);

        public async Task PieceVerified(InfoHash infoHash, int pieceIndex)
            => await Clients.All.PieceVerified(infoHash, pieceIndex);

        public async Task PeerConnected(InfoHash infoHash)
            => await Clients.All.PeerConnected(infoHash);

        public async Task PeerDisconnected(InfoHash infoHash)
            => await Clients.All.PeerDisconnected(infoHash);
    }
}
