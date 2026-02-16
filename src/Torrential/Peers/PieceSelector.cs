using Microsoft.Extensions.Logging;

namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager, PieceReservationService pieceReservationService, ILogger<PieceSelector> logger)
    {
        private const int RESERVATION_LENGTH_SECONDS = 30;

        public async Task<PieceSuggestionResult> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetVerificationBitfield(infohash, out var myBitfield))
                return PieceSuggestionResult.NoMorePieces;

            // Retrieve reservation bitfield and piece availability for rarest-first selection.
            // Both are optional --- the algorithm degrades gracefully if either is missing.
            bitfieldManager.TryGetPieceReservationBitfield(infohash, out var reservationBitfield);
            bitfieldManager.TryGetPieceAvailability(infohash, out var availability);

            // Yield to prevent herding when many peers call this at the same instant.
            // Previous: Task.Delay(Random.Shared.Next(0, 250)) allocated a Timer + Task per call.
            // Task.Yield() is zero-allocation on the hot path (just reschedules the continuation).
            await Task.Yield();

            // The suggestion algorithm considers:
            //   - peer bitfield (only suggest pieces the peer has)
            //   - reservation bitfield (skip already-reserved pieces)
            //   - availability counts (prefer rarest pieces)
            // This is a single O(n/8) pass with zero heap allocations.
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield, reservationBitfield, availability);
            if (!suggestedPiece.Index.HasValue)
                return suggestedPiece;

            var suggestedIndex = suggestedPiece.Index.Value;

            // As we approach 100% completion, reduce reservation length for faster endgame.
            // This is a simple linear function that reduces the reservation length as we approach 100%.
            var reservationLengthSeconds = RESERVATION_LENGTH_SECONDS;
            if (myBitfield.CompletionRatio > 0.80)
                reservationLengthSeconds = (int)(RESERVATION_LENGTH_SECONDS - ((RESERVATION_LENGTH_SECONDS - 1) * myBitfield.CompletionRatio));

            // There is a small race window where two peers may select the same unreserved piece.
            // One will win the reservation; the other gets a null result and retries.
            // This is rare and at most one retry, unlike the old approach where many retries
            // were needed because suggestions ignored reservations entirely.
            var reserved = await pieceReservationService.TryReservePiece(infohash, suggestedIndex, reservationLengthSeconds);
            if (!reserved)
            {
                logger.LogDebug("Piece already reserved - {Index}", suggestedIndex);
                return new PieceSuggestionResult(null, suggestedPiece.MorePiecesAvailable);
            }

            return suggestedPiece;
        }
    }
}
