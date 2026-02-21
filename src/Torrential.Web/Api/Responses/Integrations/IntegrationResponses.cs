using System.Text.Json.Serialization;

namespace Torrential.Web.Api.Responses.Integrations;

public class IntegrationVm
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Enabled { get; set; }
    public string TriggerEvent { get; set; } = "";
    public string Configuration { get; set; } = "{}";
}

public class IntegrationListResponse : IDataResponse<IntegrationVm[]>, IErrorResponse
{
    public IntegrationVm[]? Data { get; private set; }
    public ErrorData? Error { get; private set; }

    [JsonConstructor]
    private IntegrationListResponse(IntegrationVm[] data, ErrorData? error)
    {
        Data = data;
        Error = error;
    }

    public IntegrationListResponse(IntegrationVm[] data)
    {
        Data = data;
    }

    public static IntegrationListResponse ErrorResponse(ErrorCode errorCode) =>
        new([], new() { Code = errorCode });
}

public class IntegrationGetResponse : IDataResponse<IntegrationVm>, IErrorResponse
{
    public IntegrationVm? Data { get; private set; }
    public ErrorData? Error { get; private set; }

    [JsonConstructor]
    private IntegrationGetResponse(IntegrationVm data, ErrorData? error)
    {
        Data = data;
        Error = error;
    }

    public IntegrationGetResponse(IntegrationVm data)
    {
        Data = data;
    }

    public static IntegrationGetResponse ErrorResponse(ErrorCode errorCode) =>
        new(null!, new() { Code = errorCode });
}
