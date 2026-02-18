namespace Torrential.Web.Api.Requests.Settings
{
    public class ConnectionSettingsUpdateRequest
    {
        public required int MaxConnectionsPerTorrent { get; init; }
        public required int MaxConnectionsGlobal { get; init; }
        public required int MaxHalfOpenConnections { get; init; }
    }
}
