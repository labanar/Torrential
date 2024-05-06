namespace Torrential.Web.Api.Requests.Settings
{
    public class FileSettingsUpdateRequest
    {
        public required string DownloadPath { get; init; }
        public required string CompletedPath { get; init; }
    }
}
