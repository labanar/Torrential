using Microsoft.Extensions.Logging;

namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager, PieceReservationService pieceReservationService, ILogger<PieceSelector> logger)
    {
        private const int RESERVATION_LENGTH_SECONDS = 30;
        public async Task<PieceSuggestionResult> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield, PeerId peerId)
        {
            if (!bitfieldManager.TryGetVerificationBitfield(infohash, out var myBitfield))
                return PieceSuggestionResult.NoMorePieces;


            //Jitter
            await Task.Delay(Random.Shared.Next(0, 250));

            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            if (!suggestedPiece.Index.HasValue)
                return suggestedPiece;


            var suggestedIndex = suggestedPiece.Index.Value;

            //As we approach 100% completion, we can be more aggressive in our piece selection
            //meaning we can reduce the reservation length
            //This is a simple linear function that reduces the reservation length as we approach 100%
            var reservationLengthSeconds = RESERVATION_LENGTH_SECONDS;
            if (myBitfield.CompletionRatio > 0.80)
                reservationLengthSeconds = (int)(RESERVATION_LENGTH_SECONDS - ((RESERVATION_LENGTH_SECONDS - 1) * myBitfield.CompletionRatio));

            var reserved = pieceReservationService.TryReservePiece(infohash, suggestedIndex, peerId, reservationLengthSeconds);
            if (!reserved)
            {
                logger.LogDebug("Piece already reserved - {Index}", suggestedIndex);
                return new PieceSuggestionResult(null, suggestedPiece.MorePiecesAvailable);
            };

            return suggestedPiece;
        }
    }
}
