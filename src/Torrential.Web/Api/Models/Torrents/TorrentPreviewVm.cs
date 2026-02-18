namespace Torrential.Web.Api.Models.Torrents
{
    public class TorrentPreviewVm
    {
        public required string Name { get; init; }
        public required string InfoHash { get; init; }
        public required long TotalSizeBytes { get; init; }
        public required TorrentPreviewFileVm[] Files { get; init; }
    }

    public class TorrentPreviewFileVm
    {
        public required long Id { get; init; }
        public required string Filename { get; init; }
        public required long SizeBytes { get; init; }
        public required bool DefaultSelected { get; init; }
    }
}
