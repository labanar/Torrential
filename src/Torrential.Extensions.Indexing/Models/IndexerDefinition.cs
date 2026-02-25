namespace Torrential.Extensions.Indexing.Models;

public class IndexerDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required IndexerType Type { get; set; }
    public required string BaseUrl { get; set; }
    public required AuthMode AuthMode { get; set; }
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;
}

public enum IndexerType
{
    Torznab,
    Custom
}

public enum AuthMode
{
    None,
    ApiKey,
    BasicAuth
}
