namespace Torrential.Web.Api.Requests.Settings
{
    public class GlobalTorrentSettingsUpdateRequest
    {
        public required int MaxConnections { get; init; }
    }
}
