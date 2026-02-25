namespace Torrential.Extensions.Indexing.Models;

public class SearchRequest
{
    public required string Query { get; set; }
    public string? Category { get; set; }
    public int? Limit { get; set; }
}
