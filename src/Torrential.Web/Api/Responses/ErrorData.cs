namespace Torrential.Web.Api.Responses
{
    public class ErrorData
    {
        public required ErrorCode Code { get; init; }
        public string Message => GetErrorMessage(Code);

        private static string GetErrorMessage(ErrorCode errorCode)
        {
            return errorCode switch
            {
                _ => $"Unknown Error ({errorCode})",
            };
        }
    }
}
