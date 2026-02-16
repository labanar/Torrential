namespace Torrential.Peers
{
    public class PieceReservationService(BitfieldManager bitfieldManager)
    {
        public Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex, float reservationLengthSeconds = 10)
        {
            if (!bitfieldManager.TryGetPieceReservationBitfield(infoHash, out var bitfield))
                return Task.FromResult(false);

            if (bitfield.HasPiece(pieceIndex)) return Task.FromResult(false);
            bitfield.MarkHave(pieceIndex);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(reservationLengthSeconds));
                bitfield.UnmarkHave(pieceIndex);
            });

            return Task.FromResult(true);
        }
    }
}
