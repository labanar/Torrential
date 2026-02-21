namespace Torrential.Web.Api.Requests.Integrations;

public class IntegrationCreateRequest
{
    public required string Name { get; set; }
    public required string Type { get; set; } // "Slack", "Discord", "Exec"
    public required string TriggerEvent { get; set; } // "torrent_complete", "torrent_added", etc.
    public bool Enabled { get; set; } = true;
    public string Configuration { get; set; } = "{}";
}

public class IntegrationUpdateRequest
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string TriggerEvent { get; set; }
    public bool Enabled { get; set; }
    public string Configuration { get; set; } = "{}";
}
