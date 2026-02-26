using Torrential.Models;

namespace Torrential.Extensions.Indexing.Api;

// --- Requests ---

public class CreateIndexerRequest
{
    public required string Name { get; set; }
    public required IndexerType Type { get; set; }
    public required string BaseUrl { get; set; }
    public required AuthMode AuthMode { get; set; }
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; } = true;
}

public class UpdateIndexerRequest
{
    public required string Name { get; set; }
    public required IndexerType Type { get; set; }
    public required string BaseUrl { get; set; }
    public required AuthMode AuthMode { get; set; }
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool Enabled { get; set; } = true;
}

public class IndexerSearchRequest
{
    public required string Query { get; set; }
    public string? Category { get; set; }
    public int? Limit { get; set; }
}

// --- Response view models ---

public class IndexerVm
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required string BaseUrl { get; set; }
    public required string AuthMode { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset DateAdded { get; set; }
}

public class SearchResultVm
{
    public required string Title { get; set; }
    public long SizeBytes { get; set; }
    public int Seeders { get; set; }
    public int Leechers { get; set; }
    public string? InfoHash { get; set; }
    public string? DownloadUrl { get; set; }
    public string? DetailsUrl { get; set; }
    public string? Category { get; set; }
    public DateTimeOffset? PublishDate { get; set; }
    public Guid? IndexerId { get; set; }
    public string? IndexerName { get; set; }
    public MetadataVm? Metadata { get; set; }
}

public class MetadataVm
{
    public string? Description { get; set; }
    public string? ExternalId { get; set; }
    public string? ArtworkUrl { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
}
