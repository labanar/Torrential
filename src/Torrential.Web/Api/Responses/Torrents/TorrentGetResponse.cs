using System.Text.Json.Serialization;
using Torrential.Web.Api.Models;

namespace Torrential.Web.Api.Responses.Torrents
{
    public class TorrentGetResponse : IDataResponse<TorrentSummaryVm>, IErrorResponse
    {
        public static TorrentGetResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private TorrentGetResponse(TorrentSummaryVm data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public TorrentGetResponse(TorrentSummaryVm? data)
        {
            Data = data;
        }

        private TorrentGetResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public TorrentSummaryVm? Data { get; }
        public ErrorData? Error { get; set; }
    }
}
