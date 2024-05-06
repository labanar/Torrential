using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses
{
    public sealed class ActionResponse : IDataResponse<ActionData>, IErrorResponse
    {
        public ErrorData? Error { get; }
        public ActionData Data { get; }


        [JsonConstructor]
        private ActionResponse(ActionData data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public static ActionResponse ErrorResponse(ErrorCode errorCode) => new(ActionData.Failure, new() { Code = errorCode });
        public static ActionResponse SuccessResponse => new(ActionData.Successful, null);

    }


    public class ActionData
    {
        public required bool Success { get; init; }

        public static ActionData Successful => new() { Success = true };
        public static ActionData Failure => new() { Success = false };
    }
}

