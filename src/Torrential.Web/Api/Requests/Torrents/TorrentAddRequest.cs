namespace Torrential.Web.Api.Requests.Torrents
{
    public class TorrentAddRequest
    {
        public required IFormFile File { get; init; }
        public long[]? SelectedFileIds { get; init; }
    }
}
