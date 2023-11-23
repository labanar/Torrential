using Torrential.Files;
using Torrential.Peers;
using Torrential.Trackers;

namespace Torrential.Torrents
{
    public class TorrentRunner(TorrentMetadataCache metaCache, IEnumerable<ITrackerClient> trackerClients, IPeerService peerService, PeerManager peerMgr, BitfieldManager bitfieldMgr, IFileSegmentSaveService segmentSaveService, PieceSelector pieceSelector)
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

                await peerMgr.ConnectToPeers(infoHash, announceResponse, 50);
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
            //Send in the runtime deps here
            //Piece selection strategy
            //Piece queue
            //Rate limiting service

            //After a piece goes through the verification queue, we need to update our bitfield
            //We also need to broadcast to each peer that we now have this new piece
            //This is after an ENTIRE piece is downloaded and verified (not a segment of a piece, but the full piece)
            var processor = peer.Process(meta, bitfieldMgr, segmentSaveService, cancellationToken);

            //await peer.SendBitfield(new Bitfield2(meta.NumberOfPieces));
            while (peer.State.Bitfield == null)
                await Task.Delay(100);

            await peer.SendIntereted();
            while (peer.State.AmChoked)
                await Task.Delay(100);


            while (!peer.State.AmChoked)
            {
                //Start asking for pieces, wait for us to get a piece back then ask for the next piece
                var idx = pieceSelector.SuggestNextPiece(meta.InfoHash, peer.State.Bitfield);
                if (idx == null)
                {
                    await Task.Delay(250);
                    continue;
                }

                var requestSize = (int)Math.Pow(2, 14);
                var remainder = (int)meta.PieceSize;
                while (remainder > 0)
                {
                    var offset = (int)meta.PieceSize - remainder;
                    await peer.SendPieceRequest(idx.Value, offset, requestSize);
                    remainder -= requestSize;
                }

                //TODO - this responsibility should be handled after we verify that the piece hash is good
                //For now I'll artificially set the piece to high in our bitfield
                if (bitfieldMgr.TryGetBitfield(meta.InfoHash, out var myBitfield))
                    myBitfield.MarkHave(idx.Value);
            }

            await processor;
        }
    }
}
