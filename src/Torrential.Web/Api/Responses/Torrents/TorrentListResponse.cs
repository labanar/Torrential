using System.Text.Json.Serialization;
using Torrential.Web.Api.Models;

namespace Torrential.Web.Api.Responses.Torrents
{
    public class TorrentListResponse : IDataResponse<TorrentSummaryVm[]>, IErrorResponse
    {
        public static TorrentListResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private TorrentListResponse(TorrentSummaryVm[] data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public TorrentListResponse(TorrentSummaryVm[]? data)
        {
            Data = data;
        }

        private TorrentListResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public TorrentSummaryVm[]? Data { get; }
        public ErrorData? Error { get; set; }
    }

}
