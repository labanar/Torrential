namespace Torrential.Web.Api.Requests.Torrents
{
    public class TorrentAddFromUrlRequest
    {
        public required string Url { get; init; }
        public Guid? IndexerId { get; init; }
        public long[]? SelectedFileIds { get; init; }
        public string? CompletedPath { get; init; }
        public int? DesiredSeedTimeDays { get; init; }
    }
}
