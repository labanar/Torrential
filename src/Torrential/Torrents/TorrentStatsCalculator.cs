using System.Threading.Channels;

namespace Torrential.Torrents
{
    public class TorrentStatsCalculator : IAsyncDisposable
    {
        private readonly TimeSpan rateCalculationWindow = TimeSpan.FromSeconds(10);
        private long bytesAccumulated = 0;
        private DateTime windowStartTime;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processTask;
        private readonly Channel<int> updateChannel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

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

            if (elapsedTime > rateCalculationWindow.TotalSeconds)
            {
                bytesAccumulated = 0;
                return 0;
            }

            var windowElapsedTime = Math.Min(elapsedTime, rateCalculationWindow.TotalSeconds);
            return windowElapsedTime > 0 ? (bytesAccumulated / windowElapsedTime / 2) : 0;
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();

            try
            {
                await _processTask;
            }
            catch
            {

            }

            updateChannel.Writer.Complete();
        }
    }

    public readonly struct TorrentDataRecieved
    {
        public int BytesReceived { get; }
        public DateTime Timestamp { get; }

        public TorrentDataRecieved(int bytesReceived, DateTime timestamp)
        {
            BytesReceived = bytesReceived;
            Timestamp = timestamp;
        }
    }
}
