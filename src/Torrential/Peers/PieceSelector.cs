namespace Torrential.Peers
{
    public class PieceSelector(BitfieldManager bitfieldManager)
    {
        public int? SuggestNextPiece(InfoHash infohash, Bitfield peerBitfield)
        {
            if (!bitfieldManager.TryGetBitfield(infohash, out var myBitfield))
                return null;


            return myBitfield.SuggestPieceToDownload(peerBitfield);
        }
    }
}
