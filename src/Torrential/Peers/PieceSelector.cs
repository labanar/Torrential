using Microsoft.Extensions.Logging;

namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager, PieceReservationService pieceReservationService, ILogger<PieceSelector> logger)
    {
        public async Task<PieceSuggestionResult> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetVerificationBitfield(infohash, out var myBitfield))
                return PieceSuggestionResult.NoMorePieces;

            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            if (!suggestedPiece.Index.HasValue)
                return suggestedPiece;


            var suggestedIndex = suggestedPiece.Index.Value;

            //As we approach 100% completion, we can be more aggressive in our piece selection
            //meaning we can reduce the reservation length
            //This is a simple linear function that reduces the reservation length as we approach 100%
            var reservationLengthSeconds = 10;
            if (myBitfield.CompletionRatio > 0.80)
                reservationLengthSeconds = (int)(10 - (9 * myBitfield.CompletionRatio));

            var reserved = await pieceReservationService.TryReservePiece(infohash, suggestedIndex, reservationLengthSeconds);
            if (!reserved)
            {
                logger.LogDebug("Piece already reserved - {Index}", suggestedIndex);
                return new PieceSuggestionResult(null, suggestedPiece.MorePiecesAvailable);
            };

            return suggestedPiece;
        }
    }
}
