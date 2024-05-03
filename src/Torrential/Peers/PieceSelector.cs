using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager, PieceReservationService pieceReservationService, ILogger<PieceSelector> logger)
    {
        //Put bitfields on reservation
        private ConcurrentDictionary<InfoHash, SemaphoreSlim> _semaphores = [];

        public async Task<PieceSuggestionResult> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetBitfield(infohash, out var myBitfield))
                return PieceSuggestionResult.NoMorePieces;

            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            if (!suggestedPiece.Index.HasValue)
                return suggestedPiece;


            var suggestedIndex = suggestedPiece.Index.Value;


            //As we approach 100% completion, we can be more aggressive in our piece selection
            //meaning we can reduce the reservation length
            //This is a simple linear function that reduces the reservation length as we approach 100%
            var reservationLengthSeconds = 10;
            if(myBitfield.CompletionRatio > 0.80)
                reservationLengthSeconds = (int)(10 - (9 * myBitfield.CompletionRatio));

            var reserved = await pieceReservationService.TryReservePiece(infohash, suggestedIndex, reservationLengthSeconds);
            if (!reserved)
            {
                logger.LogWarning("Piece already reserved - {Index}", suggestedIndex);
                return new PieceSuggestionResult(null, suggestedPiece.MorePiecesAvailable);
            };

            return suggestedPiece;
        }
    }

    public class PieceReservationService(TorrentMetadataCache metaCache)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _bitfields = new ConcurrentDictionary<InfoHash, Bitfield>();
        public async Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex, float reservationLengthSeconds = 10)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return false;

            var bitfield = _bitfields.GetOrAdd(infoHash, (_) =>
            {
                return new Bitfield(meta.NumberOfPieces);
            });

            if (bitfield.HasPiece(pieceIndex)) return false;
            bitfield.MarkHave(pieceIndex);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(reservationLengthSeconds));
                await bitfield.UnmarkHaveAsync(pieceIndex, CancellationToken.None);
            });

            return true;
        }
    }
}
