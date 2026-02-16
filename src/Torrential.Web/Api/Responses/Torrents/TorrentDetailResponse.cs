using System.Text.Json.Serialization;
using Torrential.Web.Api.Models;

namespace Torrential.Web.Api.Responses.Torrents
{
    public class TorrentDetailResponse : IDataResponse<TorrentDetailVm>, IErrorResponse
    {
        public static TorrentDetailResponse ErrorResponse(ErrorCode errorCode) => new(errorCode);

        [JsonConstructor]
        private TorrentDetailResponse(TorrentDetailVm data, ErrorData? error)
        {
            Data = data;
            Error = error;
        }

        public TorrentDetailResponse(TorrentDetailVm? data)
        {
            Data = data;
        }

        private TorrentDetailResponse(ErrorCode errorCode)
        {
            Error = new() { Code = errorCode };
        }

        public TorrentDetailVm? Data { get; }
        public ErrorData? Error { get; set; }
    }
}
