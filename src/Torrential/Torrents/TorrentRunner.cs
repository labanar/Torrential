using Microsoft.Extensions.Logging;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Trackers;

namespace Torrential.Torrents
{
    public class TorrentRunner(ILogger<TorrentRunner> logger,
                               TorrentMetadataCache metaCache,
                               IEnumerable<ITrackerClient> trackerClients,
                               IPeerService peerService,
                               PeerManager peerMgr,
                               BitfieldManager bitfieldMgr,
                               IFileSegmentSaveService segmentSaveService,
                               PieceSelector pieceSelector)
    {
        public async Task Run(InfoHash infoHash, CancellationToken cancellationToken)
        {
            metaCache.TryGet(infoHash, out var meta);
            foreach (var tracker in trackerClients)
            {
                if (!tracker.IsValidAnnounceForClient(meta.AnnounceList.First())) continue;
                var announceResponse = await tracker.Announce(new AnnounceRequest
                {
                    InfoHash = meta.InfoHash,
                    PeerId = peerService.Self.Id,
                    Url = meta.AnnounceList.First(),
                    NumWant = 50
                });

                await peerMgr.ConnectToPeers(infoHash, announceResponse, 50, cancellationToken);
                bitfieldMgr.Initialize(meta.InfoHash, meta.NumberOfPieces);

                var tasks = new List<Task>();
                foreach (var peer in peerMgr.ConnectedPeers[meta.InfoHash])
                {
                    tasks.Add(InitiatePeer(meta, peer, cancellationToken));
                }
                await Task.WhenAll(tasks);
            }
        }

        private async Task InitiatePeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken cancellationToken)
        {
            var processor = peer.Process(meta, bitfieldMgr, segmentSaveService, cancellationToken);

            //await peer.SendBitfield(new Bitfield2(meta.NumberOfPieces));
            logger.LogInformation("Waiting for bitfield from peer");
            while (peer.State.PeerBitfield == null && !cancellationToken.IsCancellationRequested)
                await Task.Delay(100);

            logger.LogInformation("Sending interested to peer");    
            await peer.SendIntereted();

            logger.LogInformation("Waiting for unchoke from peer");
            while (peer.State.AmChoked && !cancellationToken.IsCancellationRequested)
                await Task.Delay(100);


            logger.LogInformation("Starting piece selection");
            while (!peer.State.AmChoked && !cancellationToken.IsCancellationRequested)
            {
                //Start asking for pieces, wait for us to get a piece back then ask for the next piece
                var idx = await pieceSelector.SuggestNextPieceAsync(meta.InfoHash, peer.State.PeerBitfield);
                if (idx == null)
                {
                    await Task.Delay(50);
                    continue;
                }

                logger.LogInformation("Requesting {Piece} from peer", idx);
                var requestSize = (int)Math.Pow(2, 14);
                var remainder = (int)meta.PieceSize;
                while (remainder > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var offset = (int)meta.PieceSize - remainder;
                    await peer.SendPieceRequest(idx.Value, offset, requestSize);
                    remainder -= requestSize;
                }
            }

            await processor;
        }
    }
}
