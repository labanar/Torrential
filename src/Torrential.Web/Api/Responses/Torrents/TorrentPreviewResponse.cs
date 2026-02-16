using System.Text.Json.Serialization;
using Torrential.Web.Api.Models.Torrents;

namespace Torrential.Web.Api.Responses.Torrents
{
    public class TorrentPreviewResponse : IDataResponse<TorrentPreviewVm>, IErrorResponse
    {
        public static TorrentPreviewResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private TorrentPreviewResponse(TorrentPreviewVm data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public TorrentPreviewResponse(TorrentPreviewVm? data)
        {
            Data = data;
        }

        private TorrentPreviewResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public TorrentPreviewVm? Data { get; }
        public ErrorData? Error { get; set; }
    }
}
