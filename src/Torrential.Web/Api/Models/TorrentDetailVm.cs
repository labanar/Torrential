namespace Torrential.Web.Api.Models;

public class TorrentDetailVm
{
    public required string InfoHash { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required float Progress { get; init; }
    public required float TotalSizeBytes { get; init; }
    public required long BytesDownloaded { get; init; }
    public required long BytesUploaded { get; init; }
    public required double DownloadRate { get; init; }
    public required double UploadRate { get; init; }
    public required IEnumerable<PeerSummaryVm> Peers { get; init; }
    public required BitfieldVm Bitfield { get; init; }
    public required IEnumerable<TorrentFileVm> Files { get; init; }
}

public class BitfieldVm
{
    public required int PieceCount { get; init; }
    public required int HaveCount { get; init; }
    public required string Bitfield { get; init; }
}

public class TorrentFileVm
{
    public required long Id { get; init; }
    public required string Filename { get; init; }
    public required long Size { get; init; }
    public required bool IsSelected { get; init; }
}
