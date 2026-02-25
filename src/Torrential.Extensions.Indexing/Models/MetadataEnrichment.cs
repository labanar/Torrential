namespace Torrential.Extensions.Indexing.Models;

public class MetadataEnrichment
{
    public string? Description { get; set; }
    public string? ExternalId { get; set; }
    public string? ArtworkUrl { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
}

public class EnrichedSearchResult
{
    public required TorrentSearchResult Result { get; set; }
    public MetadataEnrichment? Metadata { get; set; }
}
