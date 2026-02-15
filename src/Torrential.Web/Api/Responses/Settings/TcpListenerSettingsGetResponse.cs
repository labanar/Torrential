using System.Text.Json.Serialization;
using Torrential.Application.Data;

namespace Torrential.Web.Api.Responses.Settings;

public class TcpListenerSettingsGetResponse : IDataResponse<TcpListenerSettings>, IErrorResponse
{
    public TcpListenerSettings? Data { get; private set; }
    public ErrorData? Error { get; private set; }

    [JsonConstructor]
    private TcpListenerSettingsGetResponse(TcpListenerSettings data, ErrorData? error)
    {
        Data = data;
        Error = error;
    }

    public TcpListenerSettingsGetResponse(TcpListenerSettings data)
    {
        Data = data;
    }

    private TcpListenerSettingsGetResponse(ErrorCode errorCode)
    {
        Error = new() { Code = errorCode };
    }
}
