namespace Torrential.Web.Api.Requests.Torrents;

public class FileSelectionRequest
{
    public required long[] FileIds { get; init; }
}
