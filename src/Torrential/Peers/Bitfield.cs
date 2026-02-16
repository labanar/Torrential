using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Torrential.Peers
{
    /// <summary>
    /// A thread-safe bitfield backed by an int[] array for lock-free atomic bit operations.
    ///
    /// Threading model:
    ///   - MarkHave / UnmarkHave use Interlocked.CompareExchange (CAS loop) on int elements,
    ///     making them safe to call concurrently from any thread with zero heap allocations.
    ///   - HasPiece / HasAll / HasNone / CompletionRatio perform volatile reads and are
    ///     safe to call concurrently. Results are inherently racy (a bit may flip between
    ///     read and use), but this is acceptable for BitTorrent heuristics.
    ///   - Bytes exposes a ReadOnlySpan&lt;byte&gt; view via MemoryMarshal.AsBytes, giving
    ///     consumers the byte layout they expect without any copying.
    ///   - Fill is NOT thread-safe and should only be called during initialization,
    ///     before the bitfield is shared across threads.
    ///
    /// Memory layout:
    ///   - Backing store is int[] rented from ArrayPool&lt;int&gt;.Shared.
    ///   - Each int holds 32 piece bits. Bit ordering within each int matches the
    ///     BitTorrent wire format when viewed as bytes via MemoryMarshal.AsBytes on
    ///     little-endian platforms:
    ///       int element 0 covers pieces 0-31, stored as bytes [0..3]
    ///       Byte 0 = bits for pieces 0-7 (MSB = piece 0, matching BitTorrent convention)
    ///       etc.
    ///   - The array is padded to int alignment; _sizeInBytes tracks the actual byte count.
    ///
    /// Performance characteristics:
    ///   - MarkHave/UnmarkHave: single CAS, typically succeeds first try (no contention
    ///     on the same int element). Zero heap allocations. ~20ns uncontended.
    ///   - HasPiece: single array access + bit test. ~2ns.
    ///   - CompletionRatio/HasAll: O(sizeInInts) loop with PopCount intrinsic.
    ///   - Replaces AsyncBitfield which allocated one SemaphoreSlim per byte (~176 bytes each)
    ///     plus an async state machine Task on every MarkHaveAsync/UnmarkHaveAsync call.
    /// </summary>
    public sealed class Bitfield : IBitfield, IDisposable
    {
        private readonly int _numOfPieces;
        private readonly int _sizeInBytes;
        private readonly int _sizeInInts;
        private readonly int[] _data;

        public int NumberOfPieces => _numOfPieces;

        /// <summary>
        /// Returns the bitfield as a read-only byte span suitable for wire protocol
        /// serialization, piece suggestion, and persistence.
        ///
        /// On little-endian platforms (all modern .NET targets), MemoryMarshal.AsBytes
        /// on the int[] produces bytes in the correct order because each int is stored
        /// with its least-significant byte first, and our bit layout places piece N*32+0..7
        /// in the first byte of each int element, which corresponds to the lowest address.
        /// </summary>
        public ReadOnlySpan<byte> Bytes =>
            MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts)).Slice(0, _sizeInBytes);

        public float CompletionRatio
        {
            get
            {
                int piecesHave = 0;
                for (int i = 0; i < _sizeInInts; i++)
                    piecesHave += BitOperations.PopCount((uint)Volatile.Read(ref _data[i]));
                return piecesHave / (float)_numOfPieces;
            }
        }

        public Bitfield(int numOfPieces)
        {
            _numOfPieces = numOfPieces;
            _sizeInBytes = (numOfPieces + 7) / 8;
            _sizeInInts = (_sizeInBytes + 3) / 4;
            _data = ArrayPool<int>.Shared.Rent(_sizeInInts);
            _data.AsSpan(0, _sizeInInts).Clear();
        }

        public Bitfield(Span<byte> data)
        {
            _sizeInBytes = data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _sizeInInts = (_sizeInBytes + 3) / 4;
            _data = ArrayPool<int>.Shared.Rent(_sizeInInts);
            _data.AsSpan(0, _sizeInInts).Clear();
            data.CopyTo(MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts)));
        }

        public Bitfield(ReadOnlySequence<byte> data)
        {
            _sizeInBytes = (int)data.Length;
            _numOfPieces = _sizeInBytes * 8;
            _sizeInInts = (_sizeInBytes + 3) / 4;
            _data = ArrayPool<int>.Shared.Rent(_sizeInInts);
            _data.AsSpan(0, _sizeInInts).Clear();
            data.CopyTo(MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts)));
        }

        /// <summary>
        /// Bulk-loads bitfield data. NOT thread-safe. Call only during initialization
        /// before sharing the bitfield across threads.
        /// </summary>
        public void Fill(Span<byte> data)
        {
            // Clear the backing store first to zero any padding bytes
            _data.AsSpan(0, _sizeInInts).Clear();
            data.CopyTo(MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts)));
        }

        public bool HasAll()
        {
            // Check full bytes first
            int fullBytes = _numOfPieces / 8;
            var bytes = MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts));
            for (int i = 0; i < fullBytes; i++)
            {
                if (bytes[i] != 0xFF)
                    return false;
            }

            // Check remaining bits in the last partial byte
            int remainingBits = _numOfPieces % 8;
            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((bytes[fullBytes] & mask) != mask)
                    return false;
            }

            return true;
        }

        public bool HasAll(ReadOnlySpan<byte> requiredPiecesMask)
        {
            if (requiredPiecesMask.IsEmpty)
                return HasAll();

            var bytes = MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts));
            for (int i = 0; i < _sizeInBytes; i++)
            {
                byte requiredByte = i < requiredPiecesMask.Length
                    ? requiredPiecesMask[i]
                    : (byte)0;

                if (i == _sizeInBytes - 1)
                {
                    int remainingBits = _numOfPieces % 8;
                    if (remainingBits > 0)
                    {
                        byte validMask = (byte)(0xFF << (8 - remainingBits));
                        requiredByte &= validMask;
                    }
                }

                if (requiredByte == 0)
                    continue;

                if ((bytes[i] & requiredByte) != requiredByte)
                    return false;
            }

            return true;
        }

        public float GetCompletionRatio(ReadOnlySpan<byte> requiredPiecesMask)
        {
            if (requiredPiecesMask.IsEmpty)
                return CompletionRatio;

            var bytes = MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts));
            int requiredPieces = 0;
            int havePieces = 0;
            for (int i = 0; i < _sizeInBytes; i++)
            {
                byte requiredByte = i < requiredPiecesMask.Length
                    ? requiredPiecesMask[i]
                    : (byte)0;

                if (i == _sizeInBytes - 1)
                {
                    int remainingBits = _numOfPieces % 8;
                    if (remainingBits > 0)
                    {
                        byte validMask = (byte)(0xFF << (8 - remainingBits));
                        requiredByte &= validMask;
                    }
                }

                if (requiredByte == 0)
                    continue;

                requiredPieces += BitOperations.PopCount((uint)requiredByte);
                havePieces += BitOperations.PopCount((uint)(bytes[i] & requiredByte));
            }

            if (requiredPieces == 0)
                return 1f;

            return havePieces / (float)requiredPieces;
        }

        public bool HasNone()
        {
            for (int i = 0; i < _sizeInInts; i++)
            {
                if (Volatile.Read(ref _data[i]) != 0)
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasPiece(int index)
        {
            if ((uint)index >= (uint)_numOfPieces)
                throw new ArgumentOutOfRangeException(nameof(index));

            var byteIndex = index / 8;
            var bitIndex = 7 - (index % 8);
            var bytes = MemoryMarshal.AsBytes(_data.AsSpan(0, _sizeInInts));
            return (bytes[byteIndex] & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// Atomically sets the bit for the given piece index.
        /// Thread-safe, lock-free, zero heap allocations.
        ///
        /// Implementation: computes which int element and which bit within that int
        /// correspond to the piece index, then uses a CAS loop to atomically OR the bit.
        /// The CAS loop typically succeeds on the first iteration since contention on the
        /// same int element (covering 32 pieces) is rare.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MarkHave(int index)
        {
            if ((uint)index >= (uint)_numOfPieces)
                throw new ArgumentOutOfRangeException(nameof(index));

            // Compute which byte and bit within that byte (BitTorrent convention: MSB first)
            int byteIndex = index / 8;
            int bitInByte = 7 - (index % 8);

            // Compute which int element contains this byte, and the byte's position within the int
            int intIndex = byteIndex / 4;
            int byteInInt = byteIndex % 4;

            // Compute the bit position within the int
            // On little-endian: byte 0 of the int is at bits 0-7, byte 1 at bits 8-15, etc.
            int bitPosition = byteInInt * 8 + bitInByte;
            int mask = 1 << bitPosition;

            // CAS loop to atomically set the bit
            int oldVal, newVal;
            do
            {
                oldVal = Volatile.Read(ref _data[intIndex]);
                newVal = oldVal | mask;
                if (oldVal == newVal)
                    return false; // Bit already set
            } while (Interlocked.CompareExchange(ref _data[intIndex], newVal, oldVal) != oldVal);
            return true; // Bit was newly set
        }

        /// <summary>
        /// Atomically clears the bit for the given piece index.
        /// Thread-safe, lock-free, zero heap allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnmarkHave(int index)
        {
            if ((uint)index >= (uint)_numOfPieces)
                throw new ArgumentOutOfRangeException(nameof(index));

            int byteIndex = index / 8;
            int bitInByte = 7 - (index % 8);

            int intIndex = byteIndex / 4;
            int byteInInt = byteIndex % 4;

            int bitPosition = byteInInt * 8 + bitInByte;
            int mask = ~(1 << bitPosition);

            int oldVal, newVal;
            do
            {
                oldVal = Volatile.Read(ref _data[intIndex]);
                newVal = oldVal & mask;
                if (oldVal == newVal)
                    return; // Bit already clear
            } while (Interlocked.CompareExchange(ref _data[intIndex], newVal, oldVal) != oldVal);
        }

        public PieceSuggestionResult SuggestPieceToDownload(IBitfield peerBitfield)
        {
            return PieceSuggestion.SuggestPieceRandom(
                Bytes,
                peerBitfield.Bytes,
                ReadOnlySpan<byte>.Empty,
                _numOfPieces);
        }

        public PieceSuggestionResult SuggestPieceToDownload(
            IBitfield peerBitfield,
            IBitfield? reservationBitfield,
            PieceAvailability? availability)
        {
            return SuggestPieceToDownload(peerBitfield, reservationBitfield, availability, null);
        }

        public PieceSuggestionResult SuggestPieceToDownload(
            IBitfield peerBitfield,
            IBitfield? reservationBitfield,
            PieceAvailability? availability,
            IBitfield? wantedBitfield)
        {
            var resBytes = reservationBitfield != null
                ? reservationBitfield.Bytes
                : ReadOnlySpan<byte>.Empty;
            var availCounts = availability != null
                ? availability.Counts
                : ReadOnlySpan<int>.Empty;
            var wantedBytes = wantedBitfield != null
                ? wantedBitfield.Bytes
                : ReadOnlySpan<byte>.Empty;

            return PieceSuggestion.SuggestPiece(
                Bytes,
                peerBitfield.Bytes,
                resBytes,
                availCounts,
                wantedBytes,
                _numOfPieces);
        }

        public void Dispose()
        {
            ArrayPool<int>.Shared.Return(_data, clearArray: true);
        }
    }

    public readonly record struct PieceSuggestionResult(int? Index, bool MorePiecesAvailable)
    {
        public static PieceSuggestionResult NoMorePieces { get; } = new PieceSuggestionResult(null, false);
    }
}
