﻿using Microsoft.Extensions.Logging;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Trackers;

namespace Torrential.Torrents
{
    public class TorrentRunner(ILogger<TorrentRunner> logger,
                               BitfieldManager bitfieldMgr,
                               IFileSegmentSaveService segmentSaveService,
                               PieceSelector pieceSelector)
    {

        public async Task InitiatePeer(TorrentMetadata meta, PeerWireClient peer, CancellationToken cancellationToken)
        {
            var processor = peer.Process(meta, bitfieldMgr, segmentSaveService, cancellationToken);

            logger.LogInformation("Waiting for bitfield from peer");
            while (peer.State.PeerBitfield == null && !cancellationToken.IsCancellationRequested)
                await Task.Delay(100);

            logger.LogInformation("Sending interested to peer");    
            await peer.SendIntereted();

            logger.LogInformation("Waiting for unchoke from peer");
            while (peer.State.AmChoked && !cancellationToken.IsCancellationRequested)
                await Task.Delay(100);


            if(!bitfieldMgr.TryGetBitfield(meta.InfoHash, out var myBitfield))
            {
                logger.LogError("Failed to retrieve my bitfield");
                return;
            }

            logger.LogInformation("Starting piece selection");
            var hasMorePieces = true;
            while (!peer.State.AmChoked && !cancellationToken.IsCancellationRequested && hasMorePieces)
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
                while (remainder > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var offset = (int)meta.PieceSize - remainder;
                    await peer.SendPieceRequest(suggestion.Index.Value, offset, requestSize);
                    remainder -= requestSize;
                }
            }

            logger.LogInformation("Finished requesting pieces from peer");

            await processor;
        }
    }
}
