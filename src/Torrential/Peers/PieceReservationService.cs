using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Peers
{
    public class PieceReservationService(TorrentMetadataCache metaCache)
    {
        private ConcurrentDictionary<InfoHash, AsyncBitfield> _bitfields = [];
        public async Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex, float reservationLengthSeconds = 10)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return false;

            var bitfield = _bitfields.GetOrAdd(infoHash, (_) =>
            {
                return new AsyncBitfield(meta.NumberOfPieces);
            });

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
}
