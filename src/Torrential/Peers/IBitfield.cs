namespace Torrential.Peers
{
    public interface IBitfield
    {
        int NumberOfPieces { get; }

        ReadOnlySpan<byte> Bytes { get; }

        float CompletionRatio { get; }

        void Fill(Span<byte> data);

        bool HasAll();

        bool HasNone();

        bool HasPiece(int index);

        PieceSuggestionResult SuggestPieceToDownload(IBitfield peerBitfield);

    }


    public abstract class BitfieldBase
    {

    }
}
