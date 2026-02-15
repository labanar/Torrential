using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Torrential.Application.Events;

namespace Torrential.Application.Torrents;

public class TorrentStats
{
    private readonly ConcurrentDictionary<InfoHash, TorrentStatsCalculator> _ingressRates = [];
    private readonly ConcurrentDictionary<InfoHash, TorrentStatsCalculator> _egressRates = [];

    public async Task ClearStats(InfoHash infoHash)
    {
        if (_ingressRates.TryRemove(infoHash, out var ingressCalc))
            await ingressCalc.DisposeAsync();

        if (_egressRates.TryRemove(infoHash, out var egressCalc))
            await egressCalc.DisposeAsync();
    }

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


public class TorrentStatsMaintainer(TorrentStats rates) :
    IEventHandler<TorrentBlockDownloaded>,
    IEventHandler<TorrentBlockUploadedEvent>
{
    public async Task HandleAsync(TorrentBlockDownloaded @event, CancellationToken cancellationToken = default)
    {
        await rates.QueueDownloadRate(@event.InfoHash, @event.Length);
    }

    public async Task HandleAsync(TorrentBlockUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        await rates.QueueUploadRate(@event.InfoHash, @event.Length);
    }
}


public class TorrentThroughputRatesNotifier(IEventBus eventBus, TorrentStats rates, ILogger<TorrentThroughputRatesNotifier> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (!stoppingToken.IsCancellationRequested)
        {
            await timer.WaitForNextTickAsync(stoppingToken);
            // Note: TrackedTorrents iteration will need to be provided by a different mechanism
            // since PeerSwarm is not part of Torrential.Application
        }
    }
}
