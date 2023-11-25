using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager, PieceReservationService pieceReservationService, ILogger<PieceSelector> logger)
    {
        //Put bitfields on reservation
        private ConcurrentDictionary<InfoHash, SemaphoreSlim> _semaphores = [];


        public int? SuggestNextPiece(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetBitfield(infohash, out var myBitfield))
                return null;


            //Put a reservation on a piece

            return myBitfield.SuggestPieceToDownload(peerBitfield);
        }


        public async Task<int?> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetBitfield(infohash, out var myBitfield))
                return null;


            //Put a reservation on a piece
            var suggestedPiece = myBitfield.SuggestPieceToDownload(peerBitfield);
            if (!suggestedPiece.HasValue) return null;

            var reserved = await pieceReservationService.TryReservePiece(infohash, suggestedPiece.Value);
            if (!reserved)
            {
                logger.LogWarning("Piece already reserved - {Index}", suggestedPiece.Value);
                return null;
            };

            return suggestedPiece.Value;

            //Make a reservation

            //If successfull
            //- Return the piece as a suggestion

            //Otherwise, return null
        }
    }



    //Automatically have this remove the high flag from the piece bitfield
    public class PieceReservationService(TorrentMetadataCache metaCache)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _bitfields = new ConcurrentDictionary<InfoHash, Bitfield>();
        public async Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return false;

            var bitfield = _bitfields.GetOrAdd(infoHash, (_) =>
            {
                return new Bitfield(meta.NumberOfPieces);
            });

            if (bitfield.HasPiece(pieceIndex)) return false;

            bitfield.MarkHave(pieceIndex);

            //Make the reservation task here
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                await bitfield.UnmarkHaveAsync(pieceIndex, CancellationToken.None);
            });

            return true;
        }
    }
}
