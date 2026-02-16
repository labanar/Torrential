using System.Collections.Concurrent;

namespace Torrential.Peers
{
    /// <summary>
    /// Manages piece reservations to prevent multiple peers from downloading the same piece.
    ///
    /// Previous implementation: Task.Run + Task.Delay per reservation (closure + timer + async
    /// state machine = ~3 allocations per piece). For a 10,000-piece torrent that is 30,000+
    /// heap objects with mid-life-crisis lifetimes (survive gen0, die in gen1/2 when the timer fires).
    ///
    /// New implementation: a single long-lived background task drains a ConcurrentQueue of expiry
    /// entries at 1-second resolution. Zero per-reservation heap allocations beyond the struct entry.
    /// The queue entry is a readonly struct (24 bytes, no GC pressure).
    /// </summary>
    public sealed class PieceReservationService : IDisposable
    {
        private readonly BitfieldManager _bitfieldManager;
        private readonly ConcurrentQueue<ReservationEntry> _expiryQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _cleanupTask;

        public PieceReservationService(BitfieldManager bitfieldManager)
        {
            _bitfieldManager = bitfieldManager;
            _cleanupTask = RunCleanupLoop(_cts.Token);
        }

        /// <summary>
        /// Tries to reserve a piece. Returns synchronously (no Task allocation on the hot path).
        /// The reservation expires automatically after <paramref name="reservationLengthSeconds"/>.
        /// </summary>
        public Task<bool> TryReservePiece(InfoHash infoHash, int pieceIndex, float reservationLengthSeconds = 10)
        {
            if (!_bitfieldManager.TryGetPieceReservationBitfield(infoHash, out var bitfield))
                return Task.FromResult(false);

            if (bitfield.HasPiece(pieceIndex))
                return Task.FromResult(false);

            bitfield.MarkHave(pieceIndex);

            // Enqueue expiry. The struct is 24 bytes, written directly into the queue node.
            // No closure, no Task, no Timer allocated.
            var expiryTick = Environment.TickCount64 + (long)(reservationLengthSeconds * 1000);
            _expiryQueue.Enqueue(new ReservationEntry(expiryTick, infoHash, pieceIndex));

            return Task.FromResult(true);
        }

        /// <summary>
        /// Single background loop that processes expired reservations.
        /// Runs at ~1 second resolution which is precise enough for reservation timeouts.
        /// </summary>
        private async Task RunCleanupLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var now = Environment.TickCount64;

                // Drain all entries whose expiry time has passed.
                // The queue is FIFO and entries are enqueued in roughly chronological order,
                // so once we see an entry that hasn't expired, we can stop and re-enqueue it.
                while (_expiryQueue.TryPeek(out var entry))
                {
                    if (entry.ExpiryTick > now)
                        break; // Everything remaining is still in the future

                    _expiryQueue.TryDequeue(out _);

                    if (_bitfieldManager.TryGetPieceReservationBitfield(entry.InfoHash, out var bitfield))
                        bitfield.UnmarkHave(entry.PieceIndex);
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        /// <summary>
        /// Stack-friendly value type for the expiry queue. 24 bytes on x64.
        /// No heap allocation per entry (the ConcurrentQueue node is the only allocation,
        /// and ConcurrentQueue internally batches nodes into segments of 32).
        /// </summary>
        private readonly struct ReservationEntry(long expiryTick, InfoHash infoHash, int pieceIndex)
        {
            public readonly long ExpiryTick = expiryTick;
            public readonly InfoHash InfoHash = infoHash;
            public readonly int PieceIndex = pieceIndex;
        }
    }
}
