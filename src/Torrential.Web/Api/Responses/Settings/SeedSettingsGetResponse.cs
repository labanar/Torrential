using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings
{
    public class SeedSettingsGetResponse : IDataResponse<SeedSettings>, IErrorResponse
    {
        public SeedSettings? Data { get; private set; }
        public ErrorData? Error { get; private set; }

        [JsonConstructor]
        private SeedSettingsGetResponse(SeedSettings data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public SeedSettingsGetResponse(SeedSettings data)
        {
            Data = data;
        }

        private SeedSettingsGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }
    }
}
