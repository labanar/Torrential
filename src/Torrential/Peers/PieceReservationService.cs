using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Peers
{
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
