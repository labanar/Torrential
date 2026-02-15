namespace Torrential.Application.PieceSelection;

public interface IPieceSelector
{
    Task<PieceSuggestionResult> SuggestNextPieceAsync(InfoHash infohash, Bitfield peerBitfield);
}
