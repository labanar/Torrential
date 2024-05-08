using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Torrential.Peers;

namespace Torrential.Torrents
{
    public class TorrentRateMaintainer(TorrentThroughputRates rates) :
        IConsumer<TorrentSegmentDownloadedEvent>,
        IConsumer<TorrentSegmentUploadedEvent>
    {
        public async Task Consume(ConsumeContext<TorrentSegmentDownloadedEvent> context)
        {
            await rates.QueueDownloadRate(context.Message.InfoHash, context.Message.SegmentLength);
        }

        public async Task Consume(ConsumeContext<TorrentSegmentUploadedEvent> context)
        {
            await rates.QueueUploadRate(context.Message.InfoHash, context.Message.SegmentLength);
        }
    }

    public class TorrentThroughputRates
    {
        private ConcurrentDictionary<InfoHash, TorrentRateCalculator> _ingressRates = [];
        private ConcurrentDictionary<InfoHash, TorrentRateCalculator> _egressRates = [];


        public async Task QueueDownloadRate(InfoHash infoHash, int dataSize)
        {
            var calculator = _ingressRates.GetOrAdd(infoHash, (_) => new TorrentRateCalculator(0.9));
            await calculator.QueueUpdate(dataSize);
        }

        public async Task QueueUploadRate(InfoHash infoHash, int dataSize)
        {
            var calculator = _egressRates.GetOrAdd(infoHash, (_) => new TorrentRateCalculator(0.9));
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
    }


    public class TorrentThroughputRatesNotifier(IBus bus, PeerSwarm peerSwarm, TorrentThroughputRates rates, ILogger<TorrentThroughputRatesNotifier> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

            while (!stoppingToken.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                foreach (var infoHash in peerSwarm.PeerClients.Keys)
                {
                    var ingressRate = rates.GetIngressRate(infoHash);
                    var egressRate = rates.GetEgressRate(infoHash);

                    logger.LogInformation("Publishing throughput rates for {InfoHash} - Ingress: {IngressRate} Egress: {EgressRate}", infoHash.AsString(), ingressRate, egressRate);
                    await bus.Publish(new TorrentThroughputEvent { InfoHash = infoHash, IngressRate = ingressRate, EgressRate = egressRate });
                }
            }
        }
    }
}
