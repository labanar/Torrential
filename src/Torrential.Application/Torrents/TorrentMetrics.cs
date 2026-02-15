using System.Diagnostics.Metrics;

namespace Torrential.Application.Torrents;

public static class TorrentMetrics
{
    public const string MeterName = "Torrential.Torrents";

    internal static readonly Meter TorrentMeter = new(MeterName);

    internal static UpDownCounter<int> TORRENT_COUNT = TorrentMeter.CreateUpDownCounter<int>("torrent_count", "torrents", "Total number of torrents");
}
