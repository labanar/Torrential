using System.Threading.Channels;

namespace Torrential.Torrents
{
    public class TorrentRateCalculator : IAsyncDisposable
    {
        private double _rate = 0;
        private DateTime _lastUpdated;
        private readonly double _alpha;
        private readonly double _decayFactor = 0.95;
        private readonly TimeSpan _decayThreshold = TimeSpan.FromSeconds(5);
        private readonly Channel<TorrentRateUpdate> _updateChannel = Channel.CreateUnbounded<TorrentRateUpdate>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _processTask;

        public TorrentRateCalculator(double smoothingFactor)
        {
            _alpha = smoothingFactor;
            _lastUpdated = DateTime.UtcNow;
            _processTask = ProcessUpdates();
        }

        private async Task ProcessUpdates()
        {
            try
            {
                await foreach (var update in _updateChannel.Reader.ReadAllAsync(_cts.Token))
                {
                    ProcessUpdate(update);
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
            }
        }

        private void ProcessUpdate(TorrentRateUpdate update)
        {
            var now = DateTime.UtcNow;
            var timeDiff = (update.Timestamp - _lastUpdated).TotalSeconds;

            if (timeDiff <= 0) return; // Skip updates that are out of order or too close in time

            // Decay the rate if necessary
            if (timeDiff > _decayThreshold.TotalSeconds)
            {
                _rate = 0; // If the update is too late, reset the rate
            }
            else
            {
                _rate *= Math.Pow(_decayFactor, Math.Floor(timeDiff)); // Apply decay based on the elapsed time
            }

            double instantRate = update.TotalBytes / timeDiff;
            _rate = _rate == 0 ? instantRate : _alpha * instantRate + (1 - _alpha) * _rate;
            _lastUpdated = update.Timestamp;
        }

        public async Task QueueUpdate(int dataSize)
        {
            await _updateChannel.Writer.WriteAsync(new TorrentRateUpdate(dataSize, DateTime.UtcNow));
        }

        public double GetCurrentRate()
        {
            var now = DateTime.UtcNow;
            var timeSinceLastUpdate = now - _lastUpdated;

            if (timeSinceLastUpdate > _decayThreshold)
            {
                return 0; // Consider rate as zero if there has been no update for the duration of the decay threshold
            }

            return _rate;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _processTask;
            _cts.Dispose();
        }
    }

    public readonly struct TorrentRateUpdate
    {
        public int TotalBytes { get; }
        public DateTime Timestamp { get; }

        public TorrentRateUpdate(int totalBytes, DateTime timestamp)
        {
            TotalBytes = totalBytes;
            Timestamp = timestamp;
        }
    }
}
