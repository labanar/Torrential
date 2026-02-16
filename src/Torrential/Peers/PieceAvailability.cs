using System.Numerics;
using System.Runtime.CompilerServices;

namespace Torrential.Peers;

/// <summary>
/// Tracks how many peers in the swarm have each piece.
/// Used for rarest-first piece selection.
///
/// Thread safety: Increments/decrements use Interlocked operations so they are
/// safe to call from any thread. Reading the counts for selection is inherently
/// racy (a count may change between read and use), but that is acceptable for
/// piece selection heuristics --- the worst case is a slightly sub-optimal
/// choice, not a correctness bug.
/// </summary>
public sealed class PieceAvailability
{
    private readonly int[] _counts;
    private readonly int _numPieces;

    public int NumPieces => _numPieces;

    /// <summary>
    /// Provides read access to the availability counts.
    /// counts[i] == number of connected peers that have piece i.
    /// </summary>
    public ReadOnlySpan<int> Counts => _counts.AsSpan(0, _numPieces);

    public PieceAvailability(int numPieces)
    {
        _numPieces = numPieces;
        _counts = new int[numPieces];
    }

    /// <summary>
    /// Called when a peer's full bitfield is received. Increments the count for
    /// every piece the peer has.
    /// </summary>
    public void IncrementFromBitfield(ReadOnlySpan<byte> bitfieldBytes, int numPieces)
    {
        int pieceIndex = 0;
        for (int byteIdx = 0; byteIdx < bitfieldBytes.Length && pieceIndex < numPieces; byteIdx++)
        {
            byte b = bitfieldBytes[byteIdx];
            if (b == 0)
            {
                pieceIndex += 8;
                continue;
            }

            // Scan the 8 bits in this byte (MSB = lowest piece index, matching BitTorrent convention)
            for (int bit = 7; bit >= 0 && pieceIndex < numPieces; bit--, pieceIndex++)
            {
                if ((b & (1 << bit)) != 0)
                    Interlocked.Increment(ref _counts[pieceIndex]);
            }
        }
    }

    /// <summary>
    /// Called when a peer disconnects. Decrements the count for every piece the peer had.
    /// </summary>
    public void DecrementFromBitfield(ReadOnlySpan<byte> bitfieldBytes, int numPieces)
    {
        int pieceIndex = 0;
        for (int byteIdx = 0; byteIdx < bitfieldBytes.Length && pieceIndex < numPieces; byteIdx++)
        {
            byte b = bitfieldBytes[byteIdx];
            if (b == 0)
            {
                pieceIndex += 8;
                continue;
            }

            for (int bit = 7; bit >= 0 && pieceIndex < numPieces; bit--, pieceIndex++)
            {
                if ((b & (1 << bit)) != 0)
                {
                    // Floor at 0 to guard against double-decrement
                    int oldVal;
                    do
                    {
                        oldVal = Volatile.Read(ref _counts[pieceIndex]);
                        if (oldVal <= 0) break;
                    } while (Interlocked.CompareExchange(ref _counts[pieceIndex], oldVal - 1, oldVal) != oldVal);
                }
            }
        }
    }

    /// <summary>
    /// Called when a peer sends a Have message for a single piece.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementPiece(int pieceIndex)
    {
        if ((uint)pieceIndex < (uint)_numPieces)
            Interlocked.Increment(ref _counts[pieceIndex]);
    }

    /// <summary>
    /// Called when a peer disconnects and we need to decrement a single piece.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecrementPiece(int pieceIndex)
    {
        if ((uint)pieceIndex < (uint)_numPieces)
        {
            int oldVal;
            do
            {
                oldVal = Volatile.Read(ref _counts[pieceIndex]);
                if (oldVal <= 0) return;
            } while (Interlocked.CompareExchange(ref _counts[pieceIndex], oldVal - 1, oldVal) != oldVal);
        }
    }
}
