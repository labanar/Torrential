namespace Torrential.Web.Api.Requests.Torrents
{
    public class TorrentAddFromUrlRequest
    {
        public required string Url { get; init; }
        public long[]? SelectedFileIds { get; init; }
        public string? CompletedPath { get; init; }
    }
}
