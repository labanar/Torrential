using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings
{
    public class ConnectionSettingsGetResponse : IDataResponse<ConnectionSettings>, IErrorResponse
    {
        public ConnectionSettings? Data { get; private set; }
        public ErrorData? Error { get; private set; }

        [JsonConstructor]
        private ConnectionSettingsGetResponse(ConnectionSettings data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public ConnectionSettingsGetResponse(ConnectionSettings data)
        {
            Data = data;
        }

        private ConnectionSettingsGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }
    }
}
