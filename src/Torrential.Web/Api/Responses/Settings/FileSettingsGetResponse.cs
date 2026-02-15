using System.Text.Json.Serialization;
using Torrential.Application.Data;

namespace Torrential.Web.Api.Responses.Settings
{
    public class FileSettingsGetResponse : IDataResponse<FileSettings>, IErrorResponse
    {
        public FileSettings? Data { get; private set; }
        public ErrorData? Error { get; private set; }

        [JsonConstructor]
        private FileSettingsGetResponse(FileSettings data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public FileSettingsGetResponse(FileSettings data)
        {
            Data = data;
        }

        private FileSettingsGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }
    }
}
