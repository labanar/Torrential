using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Torrential.Torrents;

namespace Torrential.Extensions.SignalR
{
    public sealed class TorrentHubMessageDispatcher(IHubContext<TorrentHub, ITorrentClient> hubContext)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var item in TorrentEventDispatcher.EventReader.ReadAllAsync(stoppingToken))
            {
                switch (item)
                {
                    case TorrentStartedEvent _:
                        await hubContext.Clients.All.TorrentStarted(item.InfoHash);
                        break;
                    case TorrentStoppedEvent _:
                        await hubContext.Clients.All.TorrentStopped(item.InfoHash);
                        break;
                    case TorrentCompleteEvent _:
                        await hubContext.Clients.All.TorrentCompleted(item.InfoHash);
                        break;
                    case TorrentRemovedEvent _:
                        await hubContext.Clients.All.TorrentRemoved(item.InfoHash);
                        break;
                    case TorrentPieceDownloadedEvent pieceDownloaded:
                        await hubContext.Clients.All.PieceDownloaded(item.InfoHash, pieceDownloaded.PieceIndex);
                        break;
                    case TorrentPieceVerifiedEvent pieceVerified:
                        await hubContext.Clients.All.PieceVerified(item.InfoHash, pieceVerified.PieceIndex);
                        break;
                    case PeerConnectedEvent peerConnected:
                        await hubContext.Clients.All.PeerConnected(item.InfoHash);
                        break;
                    case PeerDisconnectedEvent peerDisconnected:
                        await hubContext.Clients.All.PeerDisconnected(item.InfoHash);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
