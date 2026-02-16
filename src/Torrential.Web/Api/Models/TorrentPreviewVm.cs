namespace Torrential.Web.Api.Models;

public class TorrentPreviewVm
{
    public required string Name { get; init; }
    public required long TotalSize { get; init; }
    public required TorrentPreviewFileVm[] Files { get; init; }
}

public class TorrentPreviewFileVm
{
    public required long Id { get; init; }
    public required string Filename { get; init; }
    public required long FileSize { get; init; }
}
