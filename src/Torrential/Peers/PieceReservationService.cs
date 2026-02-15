using System.Collections.Concurrent;

namespace Torrential.Peers
{
    public readonly record struct PieceReservation(PeerId Owner, DateTimeOffset ExpiresAt);

    public class PieceReservationService
    {
        private readonly ConcurrentDictionary<(InfoHash, int), PieceReservation> _reservations = new();

        public bool TryReservePiece(InfoHash infoHash, int pieceIndex, PeerId peerId, float reservationLengthSeconds = 10)
        {
            var key = (infoHash, pieceIndex);
            var now = DateTimeOffset.UtcNow;
            var newReservation = new PieceReservation(peerId, now.AddSeconds(reservationLengthSeconds));

            // Fast path: no existing reservation
            if (_reservations.TryAdd(key, newReservation))
                return true;

            // Existing reservation — check if expired
            if (_reservations.TryGetValue(key, out var existing) && existing.ExpiresAt <= now)
            {
                // Expired — try to replace it atomically
                // TryUpdate ensures we only overwrite the exact entry we checked
                return _reservations.TryUpdate(key, newReservation, existing);
            }

            return false;
        }

        public void ReleasePiece(InfoHash infoHash, int pieceIndex)
        {
            _reservations.TryRemove((infoHash, pieceIndex), out _);
        }

        public void ReleaseAllForPeer(InfoHash infoHash, PeerId peerId)
        {
            foreach (var kvp in _reservations)
            {
                if (kvp.Key.Item1 == infoHash && kvp.Value.Owner == peerId)
                    _reservations.TryRemove(kvp.Key, out _);
            }
        }

        public bool IsReservedBy(InfoHash infoHash, int pieceIndex, PeerId peerId)
        {
            if (_reservations.TryGetValue((infoHash, pieceIndex), out var reservation))
                return reservation.Owner == peerId && reservation.ExpiresAt > DateTimeOffset.UtcNow;
            return false;
        }
    }
}
