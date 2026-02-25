namespace Torrential.Extensions.Indexing.Models;

public class TorrentSearchResult
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
    public Guid IndexerId { get; set; }
    public string? IndexerName { get; set; }
}
