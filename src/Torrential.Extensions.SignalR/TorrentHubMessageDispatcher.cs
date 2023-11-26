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
                    case TorrentAddedEvent added:
                        await hubContext.Clients.All.TorrentAdded(added);
                        break;
                    case TorrentStartedEvent started:
                        await hubContext.Clients.All.TorrentStarted(started);
                        break;
                    case TorrentStoppedEvent stopped:
                        await hubContext.Clients.All.TorrentStopped(stopped);
                        break;
                    case TorrentCompleteEvent completed:
                        await hubContext.Clients.All.TorrentCompleted(completed);
                        break;
                    case TorrentRemovedEvent removed:
                        await hubContext.Clients.All.TorrentRemoved(removed);
                        break;
                    case TorrentPieceDownloadedEvent pieceDownloaded:
                        await hubContext.Clients.All.PieceDownloaded(pieceDownloaded);
                        break;
                    case TorrentPieceVerifiedEvent pieceVerified:
                        await hubContext.Clients.All.PieceVerified(pieceVerified);
                        break;
                    case PeerConnectedEvent peerConnected:
                        await hubContext.Clients.All.PeerConnected(peerConnected);
                        break;
                    case PeerDisconnectedEvent peerDisconnected:
                        await hubContext.Clients.All.PeerDisconnected(peerDisconnected);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
