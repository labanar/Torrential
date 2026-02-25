using Torrential.Extensions.Indexing.Clients;
using Torrential.Extensions.Indexing.Metadata;
using Torrential.Extensions.Indexing.Models;
using Torrential.Extensions.Indexing.Services;
using Torrential.Extensions.Indexing.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Torrential.Tests;

#region Stubs

internal class StubIndexerRepository : IIndexerRepository
{
    private readonly List<IndexerDefinition> _indexers = new();

    public void Seed(params IndexerDefinition[] indexers) => _indexers.AddRange(indexers);

    public Task<IReadOnlyList<IndexerDefinition>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<IndexerDefinition>>(_indexers.ToList());

    public Task<IndexerDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_indexers.FirstOrDefault(i => i.Id == id));

    public Task<IndexerDefinition> AddAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        _indexers.Add(indexer);
        return Task.FromResult(indexer);
    }

    public Task<IndexerDefinition?> UpdateAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        var existing = _indexers.FirstOrDefault(i => i.Id == indexer.Id);
        if (existing is null) return Task.FromResult<IndexerDefinition?>(null);

        existing.Name = indexer.Name;
        existing.Type = indexer.Type;
        existing.BaseUrl = indexer.BaseUrl;
        existing.AuthMode = indexer.AuthMode;
        existing.ApiKey = indexer.ApiKey;
        existing.Enabled = indexer.Enabled;
        return Task.FromResult<IndexerDefinition?>(existing);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var removed = _indexers.RemoveAll(i => i.Id == id);
        return Task.FromResult(removed > 0);
    }

    public Task<IReadOnlyList<IndexerDefinition>> GetEnabledAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<IndexerDefinition>>(_indexers.Where(i => i.Enabled).ToList());
}

internal class StubIndexerClient : IIndexerClient
{
    private readonly List<TorrentSearchResult> _results = new();
    private bool _failOnSearch;
    private bool _testConnectionResult = true;

    public IndexerType SupportedType => IndexerType.Torznab;

    public void SetResults(params TorrentSearchResult[] results) => _results.AddRange(results);
    public void SetFailOnSearch() => _failOnSearch = true;
    public void SetTestConnectionResult(bool result) => _testConnectionResult = result;

    public Task<IReadOnlyList<TorrentSearchResult>> SearchAsync(IndexerDefinition indexer, SearchRequest request, CancellationToken ct = default)
    {
        if (_failOnSearch) throw new HttpRequestException("Connection refused");
        // Tag results with indexer info
        foreach (var r in _results)
        {
            r.IndexerId = indexer.Id;
            r.IndexerName = indexer.Name;
        }
        return Task.FromResult<IReadOnlyList<TorrentSearchResult>>(_results.ToList());
    }

    public Task<bool> TestConnectionAsync(IndexerDefinition indexer, CancellationToken ct = default)
        => Task.FromResult(_testConnectionResult);
}

internal class StubMetadataProvider : IMetadataProvider
{
    private MetadataEnrichment? _enrichment;
    private bool _failOnEnrich;

    public void SetEnrichment(MetadataEnrichment enrichment) => _enrichment = enrichment;
    public void SetFailOnEnrich() => _failOnEnrich = true;

    public Task<MetadataEnrichment?> EnrichAsync(TorrentSearchResult result, CancellationToken ct = default)
    {
        if (_failOnEnrich) throw new InvalidOperationException("Enrichment service unavailable");
        return Task.FromResult(_enrichment);
    }
}

#endregion

public class TorznabParsingTests
{
    private static readonly IndexerDefinition TestIndexer = new()
    {
        Id = Guid.NewGuid(),
        Name = "TestIndexer",
        Type = IndexerType.Torznab,
        BaseUrl = "https://example.com",
        AuthMode = AuthMode.ApiKey,
        ApiKey = "testkey"
    };

    [Fact]
    public void ParseTorznabResponse_WithValidXml_ReturnsResults()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
                <item>
                  <title>Ubuntu 24.04 Desktop amd64</title>
                  <link>https://example.com/download/123</link>
                  <guid>https://example.com/details/123</guid>
                  <pubDate>Mon, 01 Jan 2024 00:00:00 +0000</pubDate>
                  <torznab:attr name="size" value="4700000000" />
                  <torznab:attr name="seeders" value="150" />
                  <torznab:attr name="peers" value="170" />
                  <torznab:attr name="infohash" value="abcdef1234567890abcdef1234567890abcdef12" />
                  <torznab:attr name="category" value="5000" />
                </item>
              </channel>
            </rss>
            """;

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Ubuntu 24.04 Desktop amd64", result.Title);
        Assert.Equal(4700000000L, result.SizeBytes);
        Assert.Equal(150, result.Seeders);
        Assert.Equal(20, result.Leechers); // peers (170) - seeders (150)
        Assert.Equal("abcdef1234567890abcdef1234567890abcdef12", result.InfoHash);
        Assert.Equal("https://example.com/download/123", result.DownloadUrl);
        Assert.Equal("https://example.com/details/123", result.DetailsUrl);
        Assert.Equal("5000", result.Category);
        Assert.Equal(TestIndexer.Id, result.IndexerId);
    }

    [Fact]
    public void ParseTorznabResponse_WithMultipleItems_ReturnsAll()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
                <item>
                  <title>Item One</title>
                  <link>https://example.com/download/1</link>
                  <torznab:attr name="size" value="1000" />
                  <torznab:attr name="seeders" value="10" />
                  <torznab:attr name="peers" value="15" />
                </item>
                <item>
                  <title>Item Two</title>
                  <link>https://example.com/download/2</link>
                  <torznab:attr name="size" value="2000" />
                  <torznab:attr name="seeders" value="20" />
                  <torznab:attr name="peers" value="25" />
                </item>
              </channel>
            </rss>
            """;

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Equal(2, results.Count);
        Assert.Equal("Item One", results[0].Title);
        Assert.Equal("Item Two", results[1].Title);
    }

    [Fact]
    public void ParseTorznabResponse_WithEnclosureFallback_ExtractsSizeAndUrl()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
                <item>
                  <title>Enclosure Test</title>
                  <enclosure url="https://example.com/dl/456" length="9999" type="application/x-bittorrent" />
                  <torznab:attr name="seeders" value="5" />
                  <torznab:attr name="peers" value="8" />
                </item>
              </channel>
            </rss>
            """;

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Single(results);
        Assert.Equal(9999L, results[0].SizeBytes);
        Assert.Equal("https://example.com/dl/456", results[0].DownloadUrl);
    }

    [Fact]
    public void ParseTorznabResponse_WithEmptyChannel_ReturnsEmpty()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
              </channel>
            </rss>
            """;

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseTorznabResponse_WithMalformedXml_ReturnsEmpty()
    {
        var xml = "this is not xml at all";

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseTorznabResponse_SkipsItemsWithEmptyTitle()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
              <channel>
                <item>
                  <title>   </title>
                  <link>https://example.com/download/1</link>
                </item>
                <item>
                  <title>Valid Title</title>
                  <link>https://example.com/download/2</link>
                </item>
              </channel>
            </rss>
            """;

        var results = TorznabClient.ParseTorznabResponse(xml, TestIndexer);

        Assert.Single(results);
        Assert.Equal("Valid Title", results[0].Title);
    }
}

public class IndexerSearchServiceTests
{
    private static IndexerDefinition MakeIndexer(bool enabled = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Indexer",
        Type = IndexerType.Torznab,
        BaseUrl = "https://example.com",
        AuthMode = AuthMode.ApiKey,
        ApiKey = "key",
        Enabled = enabled
    };

    [Fact]
    public async Task Search_WithNoEnabledIndexers_ReturnsEmpty()
    {
        var repo = new StubIndexerRepository();
        var client = new StubIndexerClient();
        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_ReturnsResultsFromEnabledIndexer()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeIndexer();
        repo.Seed(indexer);

        var client = new StubIndexerClient();
        client.SetResults(new TorrentSearchResult { Title = "Result 1", SizeBytes = 1000, Seeders = 5, Leechers = 2 });

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Single(results);
        Assert.Equal("Result 1", results[0].Result.Title);
    }

    [Fact]
    public async Task Search_DisabledIndexer_IsSkipped()
    {
        var repo = new StubIndexerRepository();
        repo.Seed(MakeIndexer(enabled: false));

        var client = new StubIndexerClient();
        client.SetResults(new TorrentSearchResult { Title = "Should Not Appear" });

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WhenIndexerFails_ReturnsEmptyForThatIndexer()
    {
        var repo = new StubIndexerRepository();
        repo.Seed(MakeIndexer());

        var client = new StubIndexerClient();
        client.SetFailOnSearch();

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_WithMetadataEnrichment_AddsMetadataToResults()
    {
        var repo = new StubIndexerRepository();
        repo.Seed(MakeIndexer());

        var client = new StubIndexerClient();
        client.SetResults(new TorrentSearchResult { Title = "Movie Title", SizeBytes = 5000 });

        var metadata = new StubMetadataProvider();
        metadata.SetEnrichment(new MetadataEnrichment
        {
            Description = "A great movie",
            Year = 2024,
            Genre = "Action"
        });

        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "movie" });

        Assert.Single(results);
        Assert.NotNull(results[0].Metadata);
        Assert.Equal("A great movie", results[0].Metadata!.Description);
        Assert.Equal(2024, results[0].Metadata!.Year);
        Assert.Equal("Action", results[0].Metadata!.Genre);
    }

    [Fact]
    public async Task Search_WhenEnrichmentFails_ReturnsResultWithoutMetadata()
    {
        var repo = new StubIndexerRepository();
        repo.Seed(MakeIndexer());

        var client = new StubIndexerClient();
        client.SetResults(new TorrentSearchResult { Title = "Some Torrent", SizeBytes = 1000 });

        var metadata = new StubMetadataProvider();
        metadata.SetFailOnEnrich();

        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Single(results);
        Assert.Equal("Some Torrent", results[0].Result.Title);
        Assert.Null(results[0].Metadata);
    }

    [Fact]
    public async Task TestIndexer_WithExistingIndexer_ReturnsClientResult()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeIndexer();
        repo.Seed(indexer);

        var client = new StubIndexerClient();
        client.SetTestConnectionResult(true);

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var result = await service.TestIndexerAsync(indexer.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task TestIndexer_WithNonExistentIndexer_ReturnsFalse()
    {
        var repo = new StubIndexerRepository();
        var client = new StubIndexerClient();
        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var result = await service.TestIndexerAsync(Guid.NewGuid());

        Assert.False(result);
    }
}

public class IndexerConfigurationValidationTests
{
    [Fact]
    public void IndexerDefinition_DefaultValues_AreCorrect()
    {
        var indexer = new IndexerDefinition
        {
            Name = "Test",
            Type = IndexerType.Torznab,
            BaseUrl = "https://example.com",
            AuthMode = AuthMode.ApiKey
        };

        Assert.True(indexer.Enabled);
        Assert.NotEqual(Guid.Empty, indexer.Id);
        Assert.True(indexer.DateAdded <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void SearchRequest_RequiredFields_AreSet()
    {
        var request = new SearchRequest { Query = "test query" };

        Assert.Equal("test query", request.Query);
        Assert.Null(request.Category);
        Assert.Null(request.Limit);
    }

    [Fact]
    public void MetadataEnrichment_AllFieldsOptional()
    {
        var enrichment = new MetadataEnrichment();

        Assert.Null(enrichment.Description);
        Assert.Null(enrichment.ExternalId);
        Assert.Null(enrichment.ArtworkUrl);
        Assert.Null(enrichment.Genre);
        Assert.Null(enrichment.Year);
    }
}

public class NoOpMetadataProviderTests
{
    [Fact]
    public async Task Enrich_AlwaysReturnsNull()
    {
        var provider = new NoOpMetadataProvider();
        var result = new TorrentSearchResult { Title = "Test" };

        var enrichment = await provider.EnrichAsync(result);

        Assert.Null(enrichment);
    }
}
