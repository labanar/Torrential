using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Integrations
{
    public class IntegrationHookListResponse : IDataResponse<IntegrationHookVm[]>, IErrorResponse
    {
        public static IntegrationHookListResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private IntegrationHookListResponse(IntegrationHookVm[] data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public IntegrationHookListResponse(IntegrationHookVm[]? data)
        {
            Data = data;
        }

        private IntegrationHookListResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public IntegrationHookVm[]? Data { get; }
        public ErrorData? Error { get; set; }
    }
}
