using System.Collections.Concurrent;
using Torrential.Peers.Pieces;

namespace Torrential.Peers
{
    public class BitfieldManager
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _bitfields = new ConcurrentDictionary<InfoHash, Bitfield>();

        public void Initialize(InfoHash infoHash, int numberOfPieces)
        {
            _bitfields[infoHash] = new Bitfield(numberOfPieces);
        }

        public bool TryGetBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _bitfields.TryGetValue(infoHash, out bitfield);
        }
    }
}
