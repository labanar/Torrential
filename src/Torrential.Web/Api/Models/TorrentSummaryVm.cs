﻿namespace Torrential.Web.Api.Models;

public class TorrentSummaryVm
{
    public required string InfoHash { get; init; }
    public required string Name { get; set; }
    public required float Progress { get; set; }
    public required long BytesDownloaded { get; set; }
    public required long BytesUploaded { get; set; }
    public required double DownloadRate { get; set; }
    public required double UploadRate { get; set; }
    public required float TotalSizeBytes { get; init; }
    public IEnumerable<PeerSummaryVm> Peers { get; init; } = Array.Empty<PeerSummaryVm>();
    public required string Status { get; init; }
}


public class PeerSummaryVm
{
    public required string PeerId { get; init; }
    public required string IpAddress { get; init; }
    public required int Port { get; init; }
    public required long BytesDownloaded { get; init; }
    public required long BytesUploaded { get; init; }
    public required bool IsSeed { get; init; }
    public required float Progress { get; init; }
}
