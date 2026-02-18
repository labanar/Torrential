using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings
{
    public class IntegrationsSettingsGetResponse : IDataResponse<IntegrationsSettings>, IErrorResponse
    {
        public IntegrationsSettings? Data { get; private set; }
        public ErrorData? Error { get; private set; }

        [JsonConstructor]
        private IntegrationsSettingsGetResponse(IntegrationsSettings data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public IntegrationsSettingsGetResponse(IntegrationsSettings data)
        {
            Data = data;
        }

        private IntegrationsSettingsGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }
    }
}
