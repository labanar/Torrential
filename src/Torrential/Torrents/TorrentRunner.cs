using Microsoft.Extensions.Logging;
using Torrential.Files;
using Torrential.Peers;

namespace Torrential.Torrents
{
    public class TorrentRunner(ILogger<TorrentRunner> logger,
                               BitfieldManager bitfieldMgr,
                               IFileSegmentSaveService segmentSaveService,
                               PieceSelector pieceSelector)
    {

        public async Task InitiatePeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken stoppingToken)
        {
            var processor = peer.Process(meta, bitfieldMgr, segmentSaveService, stoppingToken);

            if (!bitfieldMgr.TryGetVerificationBitfield(meta.InfoHash, out var verificationBitfield))
            {
                logger.LogInformation("Failed to retrieve verification bitfield");
                return;
            }
            await peer.SendBitfield(verificationBitfield);

            logger.LogInformation("Waiting for bitfield from peer");
            while (peer.State.PeerBitfield == null && !stoppingToken.IsCancellationRequested)
                await Task.Delay(100);

            var leechTask = LeechFromPeer(meta, peer, stoppingToken);
            await Task.WhenAll(processor, leechTask);
        }


        private async Task LeechFromPeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken stoppingToken)
        {
            if (!bitfieldMgr.TryGetDownloadBitfield(meta.InfoHash, out var downloadBitfield))
            {
                logger.LogInformation("Failed to retrieve download bitfield");
                return;
            }

            if (!bitfieldMgr.TryGetVerificationBitfield(meta.InfoHash, out var verificationBitfield))
            {
                logger.LogInformation("Failed to retrieve verification bitfield");
                return;
            }

            if (downloadBitfield.HasAll())
            {
                logger.LogInformation("Already have all pieces");
                return;
            }

            logger.LogInformation("Sending interested to peer");
            await peer.SendIntereted();


            logger.LogInformation("Waiting for unchoke from peer");
            while (peer.State.AmChoked && !stoppingToken.IsCancellationRequested)
                await Task.Delay(100);


            logger.LogInformation("Starting piece selection");
            var hasMorePieces = true;
            while (!peer.State.AmChoked && !stoppingToken.IsCancellationRequested && hasMorePieces)
            {
                //Start asking for pieces, wait for us to get a piece back then ask for the next piece
                var suggestion = await pieceSelector.SuggestNextPieceAsync(meta.InfoHash, peer.State.PeerBitfield);
                if (!suggestion.Index.HasValue)
                {
                    hasMorePieces = suggestion.MorePiecesAvailable;
                    await Task.Delay(50);
                    continue;
                }

                logger.LogInformation("Requesting {Piece} from peer", suggestion);
                var requestSize = (int)Math.Pow(2, 14);
                var remainder = (int)meta.PieceSize;
                while (remainder > 0 && !stoppingToken.IsCancellationRequested)
                {
                    var offset = (int)meta.PieceSize - remainder;
                    await peer.SendPieceRequest(suggestion.Index.Value, offset, requestSize);
                    remainder -= requestSize;
                }
            }

            logger.LogInformation("Finished requesting pieces from peer");
        }

        private async Task SeedToPeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken stoppingToken)
        {
            if (peer.State.PeerBitfield == null)
            {
                logger.LogInformation("Peer has not sent bitfield yet");
                return;
            }

            if (peer.State.PeerBitfield.HasAll())
            {
                logger.LogInformation("Peer has all pieces");
                return;
            }

            //Wait for peer to be interested then unchoke them
            while (!peer.State.PeerInterested && !stoppingToken.IsCancellationRequested)
                await Task.Delay(100);

            logger.LogInformation("Peer has shown interest, unchoking");
            await peer.SendUnchoke();

            //Wait for the peer to request a piece;
        }
    }
}
