namespace Torrential.Peers.Pieces
{

    //This strategy should ideally be able too hook into the peer event stream to be able to react/mark change in the bitfield
    public interface IPieceSelectionStrategy
    {
        public void SuggestPiece(InfoHash hash)
        {
        }
    }
}
