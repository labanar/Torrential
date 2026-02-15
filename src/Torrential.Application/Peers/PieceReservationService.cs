namespace Torrential.Application.Peers;

public class PieceReservationService(BitfieldManager bitfieldManager)
{
    public async Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex, float reservationLengthSeconds = 10)
    {
        if (!bitfieldManager.TryGetPieceReservationBitfield(infoHash, out var bitfield))
            return false;

        if (bitfield.HasPiece(pieceIndex)) return false;
        await bitfield.MarkHaveAsync(pieceIndex, CancellationToken.None);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(reservationLengthSeconds));
            await bitfield.UnmarkHaveAsync(pieceIndex, CancellationToken.None);
        });

        return true;
    }
}
