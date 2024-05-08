using System.Threading.Channels;

namespace Torrential.Torrents
{
    public class TorrentStatsCalculator : IAsyncDisposable
    {
        private readonly TimeSpan rateCalculationWindow = TimeSpan.FromSeconds(5);
        private long bytesAccumulated = 0;
        private DateTime windowStartTime;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processTask;
        private readonly Channel<int> updateChannel = Channel.CreateUnbounded<int>();
        public long TotalBytesObserved { get; private set; }

        public TorrentStatsCalculator()
        {
            windowStartTime = DateTime.UtcNow;
            _cts = new CancellationTokenSource();
            _processTask = ProcessUpdates();
        }

        private async Task ProcessUpdates()
        {
            await foreach (var bytes in updateChannel.Reader.ReadAllAsync(_cts.Token))
            {
                TotalBytesObserved += bytes;
                UpdateBytesReceived(bytes);
            }
        }

        private void UpdateBytesReceived(int bytes)
        {
            var now = DateTime.UtcNow;
            var elapsedTime = now - windowStartTime;

            if (elapsedTime > rateCalculationWindow)
            {
                bytesAccumulated = 0;  // Reset the counter if the time window has elapsed
                windowStartTime = now;  // Reset the start time of the window
            }

            bytesAccumulated += bytes;
        }

        public async Task QueueUpdate(int bytesReceived)
        {
            await updateChannel.Writer.WriteAsync(bytesReceived);
        }

        public double GetCurrentRate()
        {
            var elapsedTime = (DateTime.UtcNow - windowStartTime).TotalSeconds;
            return elapsedTime > 0 ? (bytesAccumulated / elapsedTime) : 0;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();

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
