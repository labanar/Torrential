using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Peers;

namespace Torrential.Torrents
{
    public class TorrentStatsMaintainer(TorrentStats rates) :
        IConsumer<TorrentBlockDownloaded>,
        IConsumer<TorrentBlockUploadedEvent>
    {
        public async Task Consume(ConsumeContext<TorrentBlockDownloaded> context)
        {
            await rates.QueueDownloadRate(context.Message.InfoHash, context.Message.Length);
        }

        public async Task Consume(ConsumeContext<TorrentBlockUploadedEvent> context)
        {
            await rates.QueueUploadRate(context.Message.InfoHash, context.Message.Length);
        }
    }

    public class TorrentStats
    {
        private readonly ConcurrentDictionary<InfoHash, TorrentStatsCalculator> _ingressRates = [];
        private readonly ConcurrentDictionary<InfoHash, TorrentStatsCalculator> _egressRates = [];

        public async Task QueueDownloadRate(InfoHash infoHash, int dataSize)
        {
            var calculator = _ingressRates.GetOrAdd(infoHash, (_) => new TorrentStatsCalculator());
            await calculator.QueueUpdate(dataSize);
        }

        public async Task QueueUploadRate(InfoHash infoHash, int dataSize)
        {
            var calculator = _egressRates.GetOrAdd(infoHash, (_) => new TorrentStatsCalculator());
            await calculator.QueueUpdate(dataSize);
        }

        public double GetIngressRate(InfoHash infoHash)
        {
            if (_ingressRates.TryGetValue(infoHash, out var calculator))
                return calculator.GetCurrentRate();

            return 0;
        }

        public double GetEgressRate(InfoHash infoHash)
        {
            if (_egressRates.TryGetValue(infoHash, out var calculator))
                return calculator.GetCurrentRate();

            return 0;
        }


        public long GetTotalDownloaded(InfoHash infoHash)
        {
            if (_ingressRates.TryGetValue(infoHash, out var calculator))
                return calculator.TotalBytesObserved;

            return 0;
        }

        public long GetTotalUploaded(InfoHash infoHash)
        {
            if (_egressRates.TryGetValue(infoHash, out var calculator))
                return calculator.TotalBytesObserved;

            return 0;
        }

    }


    public class TorrentThroughputRatesNotifier(IBus bus, PeerSwarm peerSwarm, TorrentStats rates, ILogger<TorrentThroughputRatesNotifier> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

            while (!stoppingToken.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                foreach (var infoHash in peerSwarm.PeerClients.Keys)
                {
                    var ingressRate = rates.GetIngressRate(infoHash);
                    var egressRate = rates.GetEgressRate(infoHash);
                    var totalDownloaded = rates.GetTotalDownloaded(infoHash);
                    var totalUploaded = rates.GetTotalUploaded(infoHash);

                    logger.LogDebug("Publishing throughput rates for {InfoHash} - Ingress: {IngressRate} Egress: {EgressRate}", infoHash.AsString(), ingressRate, egressRate);
                    await bus.Publish(new TorrentStatsEvent
                    {
                        InfoHash = infoHash,
                        DownloadRate = ingressRate,
                        UploadRate = egressRate,
                        TotalDownloaded = totalDownloaded,
                        TotalUploaded = totalUploaded
                    });
                }
            }
        }
    }
}
