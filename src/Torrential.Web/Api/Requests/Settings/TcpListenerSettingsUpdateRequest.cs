namespace Torrential.Web.Api.Requests.Settings
{
    public class TcpListenerSettingsUpdateRequest
    {
        public required bool Enabled { get; set; }
        public required int Port { get; set; }
    }
}
