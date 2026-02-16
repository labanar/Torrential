namespace Torrential.Web.Api.Requests.Torrents
{
    public class TorrentPreviewRequest
    {
        public required IFormFile File { get; init; }
    }
}
