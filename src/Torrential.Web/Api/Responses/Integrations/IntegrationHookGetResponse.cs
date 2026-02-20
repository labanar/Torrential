using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Integrations
{
    public class IntegrationHookGetResponse : IDataResponse<IntegrationHookVm>, IErrorResponse
    {
        public static IntegrationHookGetResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private IntegrationHookGetResponse(IntegrationHookVm data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public IntegrationHookGetResponse(IntegrationHookVm? data)
        {
            Data = data;
        }

        private IntegrationHookGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public IntegrationHookVm? Data { get; }
        public ErrorData? Error { get; set; }
    }
}
