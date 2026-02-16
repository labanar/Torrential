using System.Numerics;
using System.Runtime.CompilerServices;

namespace Torrential.Peers;

/// <summary>
/// Zero-allocation (hot path) piece suggestion using rarest-first selection.
///
/// Algorithm:
///   1. For each byte position, compute a "candidate" byte:
///        candidate = peerHas AND (NOT weHave) AND (NOT reserved)
///      Each set bit in candidate represents a piece the peer has, we do not have,
///      and is not currently reserved by another download.
///
///   2. Scan candidate bits and track the piece with the lowest availability count
///      (rarest-first). Ties are broken by random jitter to avoid herding where
///      multiple peers all pick the same rare piece.
///
///   3. Return the selected piece index or NoMorePieces if no candidates exist.
///
/// This approach is O(sizeInBytes) --- one pass over the bitfield bytes --- and
/// allocates nothing on the managed heap.
/// </summary>
public static class PieceSuggestion
{
    /// <summary>
    /// Suggests the best piece to download from a given peer, considering:
    /// - What the peer has (peerBitfield)
    /// - What we already have (ourBitfield)
    /// - What is already reserved (reservationBitfield, may be null)
    /// - Piece rarity across the swarm (availability, may be null for fallback)
    /// - The actual number of pieces (to mask trailing bits in the last byte)
    /// </summary>
    public static PieceSuggestionResult SuggestPiece(
        ReadOnlySpan<byte> ourBytes,
        ReadOnlySpan<byte> peerBytes,
        ReadOnlySpan<byte> reservationBytes,
        ReadOnlySpan<int> availabilityCounts,
        int numPieces)
    {
        return SuggestPiece(ourBytes, peerBytes, reservationBytes, availabilityCounts, ReadOnlySpan<byte>.Empty, numPieces);
    }

    /// <summary>
    /// Suggests the best piece to download, additionally filtering by an allowed-pieces mask.
    /// When allowedBytes is non-empty, only pieces whose bit is set in the allowed mask are considered.
    /// This enables file-selection-aware downloading: pieces outside selected files are skipped.
    /// </summary>
    public static PieceSuggestionResult SuggestPiece(
        ReadOnlySpan<byte> ourBytes,
        ReadOnlySpan<byte> peerBytes,
        ReadOnlySpan<byte> reservationBytes,
        ReadOnlySpan<int> availabilityCounts,
        ReadOnlySpan<byte> allowedBytes,
        int numPieces)
    {
        int sizeInBytes = ourBytes.Length;
        if (sizeInBytes == 0 || numPieces == 0)
            return PieceSuggestionResult.NoMorePieces;

        int bestPiece = -1;
        int bestAvailability = int.MaxValue;
        bool anyCandidate = false;

        // Random jitter seed for tie-breaking. We use a cheap counter-based approach:
        // among pieces with equal availability, we reservoir-sample with a simple counter.
        int tieCount = 0;

        for (int byteIdx = 0; byteIdx < sizeInBytes; byteIdx++)
        {
            byte peerByte = byteIdx < peerBytes.Length ? peerBytes[byteIdx] : (byte)0;
            byte ourByte = ourBytes[byteIdx];
            byte resByte = !reservationBytes.IsEmpty && byteIdx < reservationBytes.Length
                ? reservationBytes[byteIdx]
                : (byte)0;

            // candidate = peer has AND we do NOT have AND NOT reserved
            byte candidate = (byte)(peerByte & ~ourByte & ~resByte);

            // Apply allowed-pieces mask: only consider pieces belonging to selected files
            if (!allowedBytes.IsEmpty && byteIdx < allowedBytes.Length)
                candidate &= allowedBytes[byteIdx];

            if (candidate == 0)
                continue;

            // Mask off trailing bits in the last byte that are beyond numPieces
            if (byteIdx == sizeInBytes - 1)
            {
                int validBits = numPieces - (byteIdx * 8);
                if (validBits < 8)
                {
                    // Zero out the (8 - validBits) least significant bits
                    byte mask = (byte)(0xFF << (8 - validBits));
                    candidate &= mask;
                    if (candidate == 0)
                        continue;
                }
            }

            anyCandidate = true;

            // Scan set bits in candidate byte (MSB first, matching BitTorrent bit ordering)
            // We iterate from bit 7 down to bit 0
            int basePiece = byteIdx * 8;
            byte remaining = candidate;
            while (remaining != 0)
            {
                // Find the highest set bit (MSB). In BitTorrent, bit 7 of byte N = piece N*8+0.
                int highBit = 7 - BitOperations.LeadingZeroCount((uint)remaining << 24);
                int pieceIndex = basePiece + (7 - highBit);

                if (pieceIndex < numPieces)
                {
                    int avail = !availabilityCounts.IsEmpty && pieceIndex < availabilityCounts.Length
                        ? availabilityCounts[pieceIndex]
                        : 0;

                    if (avail < bestAvailability)
                    {
                        bestAvailability = avail;
                        bestPiece = pieceIndex;
                        tieCount = 1;
                    }
                    else if (avail == bestAvailability)
                    {
                        // Reservoir sampling for uniform random tie-breaking without allocation.
                        // Random.Shared.Next is thread-safe and fast.
                        tieCount++;
                        if (Random.Shared.Next(tieCount) == 0)
                            bestPiece = pieceIndex;
                    }
                }

                // Clear the bit we just processed
                remaining &= (byte)~(1 << highBit);
            }
        }

        if (!anyCandidate)
        {
            // Check if WE have all pieces (download complete) vs just no candidates from this peer
            bool weHaveAll = HasAllPieces(ourBytes, numPieces);
            return weHaveAll
                ? PieceSuggestionResult.NoMorePieces
                : new PieceSuggestionResult(null, true); // More pieces exist but this peer cannot help
        }

        return new PieceSuggestionResult(bestPiece, true);
    }

    /// <summary>
    /// Simplified suggestion without availability data --- picks a random candidate piece.
    /// Used as a fallback when no availability tracking is present.
    /// </summary>
    public static PieceSuggestionResult SuggestPieceRandom(
        ReadOnlySpan<byte> ourBytes,
        ReadOnlySpan<byte> peerBytes,
        ReadOnlySpan<byte> reservationBytes,
        int numPieces)
    {
        return SuggestPiece(ourBytes, peerBytes, reservationBytes, ReadOnlySpan<int>.Empty, numPieces);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasAllPieces(ReadOnlySpan<byte> bytes, int numPieces)
    {
        int count = 0;
        for (int i = 0; i < bytes.Length; i++)
            count += BitOperations.PopCount(bytes[i]);
        return count >= numPieces;
    }
}
