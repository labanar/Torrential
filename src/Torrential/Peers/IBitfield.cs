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

        /// <summary>
        /// Atomically sets the bit for the given piece index.
        /// Thread-safe, lock-free, zero heap allocations.
        /// </summary>
        void MarkHave(int index);

        /// <summary>
        /// Atomically clears the bit for the given piece index.
        /// Thread-safe, lock-free, zero heap allocations.
        /// </summary>
        void UnmarkHave(int index);

        PieceSuggestionResult SuggestPieceToDownload(IBitfield peerBitfield);
    }
}
