using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings;

public class GlobalTorrentSettingsGetResponse : IDataResponse<GlobalTorrentSettings>, IErrorResponse
{
    public GlobalTorrentSettings? Data { get; private set; }
    public ErrorData? Error { get; private set; }

    [JsonConstructor]
    private GlobalTorrentSettingsGetResponse(GlobalTorrentSettings data, ErrorData? error)
    {
        Data = data;
        Error = error;
    }

    public GlobalTorrentSettingsGetResponse(GlobalTorrentSettings data)
    {
        Data = data;
    }

    private GlobalTorrentSettingsGetResponse(ErrorCode errorCode)
    {
        Error = new() { Code = errorCode };
    }
}
