namespace Torrential.Integrations;

public class Integration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public IntegrationType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public required string TriggerEvent { get; set; } // e.g. "torrent_complete", "torrent_added", "torrent_started", "torrent_stopped"
    public string Configuration { get; set; } = "{}"; // JSON blob for type-specific config
}

public enum IntegrationType
{
    Slack,
    Discord,
    Exec
}
