using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings
{
    public class IntegrationSettingsGetResponse : IDataResponse<IntegrationSettings>, IErrorResponse
    {
        public IntegrationSettings? Data { get; private set; }
        public ErrorData? Error { get; private set; }

        [JsonConstructor]
        private IntegrationSettingsGetResponse(IntegrationSettings data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public IntegrationSettingsGetResponse(IntegrationSettings data)
        {
            Data = data;
        }

        private IntegrationSettingsGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }
    }
}
