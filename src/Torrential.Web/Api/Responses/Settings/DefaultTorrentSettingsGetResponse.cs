using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Settings;

public class DefaultTorrentSettingsGetResponse : IDataResponse<DefaultTorrentSettings>, IErrorResponse
{
    public DefaultTorrentSettings? Data { get; private set; }
    public ErrorData? Error { get; private set; }

    [JsonConstructor]
    private DefaultTorrentSettingsGetResponse(DefaultTorrentSettings data, ErrorData? error)
    {
        Data = data;
        Error = error;
    }

    public DefaultTorrentSettingsGetResponse(DefaultTorrentSettings data)
    {
        Data = data;
    }

    private DefaultTorrentSettingsGetResponse(ErrorCode errorCode)
    {
        Error = new() { Code = errorCode };
    }
}
