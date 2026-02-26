using Torrential.Extensions.Indexing.Clients;
using Torrential.Extensions.Indexing.Metadata;
using Torrential.Extensions.Indexing.Models;
using Torrential.Extensions.Indexing.Services;
using Torrential.Extensions.Indexing.Persistence;
using Torrential.Models;
using Torrential.Torrents;
using Torrential.Web.Api.Requests.Torrents;
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
    private bool _failOnDownload;
    private bool _testConnectionResult = true;
    private byte[]? _downloadBytes;
    private readonly IndexerType _supportedType;

    public StubIndexerClient(IndexerType supportedType = IndexerType.Torznab)
    {
        _supportedType = supportedType;
    }

    public IndexerType SupportedType => _supportedType;

    public void SetResults(params TorrentSearchResult[] results) => _results.AddRange(results);
    public void SetFailOnSearch() => _failOnSearch = true;
    public void SetFailOnDownload() => _failOnDownload = true;
    public void SetDownloadBytes(byte[] bytes) => _downloadBytes = bytes;
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

    public Task<byte[]> DownloadTorrentAsync(IndexerDefinition indexer, string downloadUrl, CancellationToken ct = default)
    {
        if (_failOnDownload) throw new HttpRequestException("Authentication failed");
        return Task.FromResult(_downloadBytes ?? Array.Empty<byte>());
    }
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

public class TorrentLeechParsingTests
{
    private static readonly IndexerDefinition TestIndexer = new()
    {
        Id = Guid.NewGuid(),
        Name = "TL",
        Type = IndexerType.TorrentLeech,
        BaseUrl = "https://www.torrentleech.org",
        AuthMode = AuthMode.Cookie,
        Username = "user",
        Password = "pass"
    };

    [Fact]
    public void ParseSearchResponse_WithValidJson_ReturnsResults()
    {
        var json = """
            {
                "numFound": 2,
                "torrentList": [
                    {
                        "fid": "241659300",
                        "filename": "Test.Torrent.2025.torrent",
                        "name": "Test Torrent 2025",
                        "addedTimestamp": "2025-12-10 23:13:44",
                        "categoryID": 31,
                        "size": 65229542385,
                        "completed": 174,
                        "seeders": 16,
                        "leechers": 8,
                        "numComments": 0,
                        "tags": ["FREELEECH"],
                        "imdbID": "",
                        "rating": 0,
                        "genres": "",
                        "download_multiplier": 0
                    },
                    {
                        "fid": "241652459",
                        "filename": "Another.Torrent.1080p.torrent",
                        "name": "Another Torrent 1080p",
                        "addedTimestamp": "2025-11-30 03:29:16",
                        "categoryID": 36,
                        "size": 3133136453,
                        "seeders": 8,
                        "leechers": 0,
                        "imdbID": "tt0096251",
                        "rating": 7,
                        "genres": "Horror, Sci-Fi, Thriller"
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Equal(2, results.Count);

        var first = results[0];
        Assert.Equal("Test Torrent 2025", first.Title);
        Assert.Equal(65229542385L, first.SizeBytes);
        Assert.Equal(16, first.Seeders);
        Assert.Equal(8, first.Leechers);
        Assert.Equal("31", first.Category);
        Assert.Equal(TestIndexer.Id, first.IndexerId);
        Assert.Equal("TL", first.IndexerName);
        Assert.Equal("https://www.torrentleech.org/download/241659300/Test.Torrent.2025.torrent", first.DownloadUrl);
        Assert.Equal("https://www.torrentleech.org/torrent/241659300", first.DetailsUrl);
        Assert.NotNull(first.PublishDate);

        var second = results[1];
        Assert.Equal("Another Torrent 1080p", second.Title);
        Assert.Equal(3133136453L, second.SizeBytes);
        Assert.Equal(8, second.Seeders);
        Assert.Equal(0, second.Leechers);
    }

    [Fact]
    public void ParseSearchResponse_WithDateParsing_ExtractsCorrectDate()
    {
        var json = """
            {
                "numFound": 1,
                "torrentList": [
                    {
                        "fid": "100",
                        "filename": "test.torrent",
                        "name": "Date Test",
                        "addedTimestamp": "2025-12-10 23:13:44",
                        "categoryID": 14,
                        "size": 1000,
                        "seeders": 1,
                        "leechers": 0
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Single(results);
        Assert.NotNull(results[0].PublishDate);
        Assert.Equal(2025, results[0].PublishDate!.Value.Year);
        Assert.Equal(12, results[0].PublishDate!.Value.Month);
        Assert.Equal(10, results[0].PublishDate!.Value.Day);
    }

    [Fact]
    public void ParseSearchResponse_WithMissingOptionalFields_HandlesGracefully()
    {
        var json = """
            {
                "numFound": 1,
                "torrentList": [
                    {
                        "name": "Minimal Torrent"
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Single(results);
        Assert.Equal("Minimal Torrent", results[0].Title);
        Assert.Equal(0, results[0].SizeBytes);
        Assert.Equal(0, results[0].Seeders);
        Assert.Equal(0, results[0].Leechers);
        Assert.Null(results[0].DownloadUrl);
        Assert.Null(results[0].DetailsUrl);
        Assert.Null(results[0].Category);
        Assert.Null(results[0].PublishDate);
    }

    [Fact]
    public void ParseSearchResponse_WithEmptyTorrentList_ReturnsEmpty()
    {
        var json = """
            {
                "numFound": 0,
                "torrentList": []
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_WithMissingTorrentList_ReturnsEmpty()
    {
        var json = """{ "numFound": 0 }""";

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_WithMalformedJson_ReturnsEmpty()
    {
        var json = "this is not json";

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_SkipsItemsWithEmptyName()
    {
        var json = """
            {
                "numFound": 2,
                "torrentList": [
                    {
                        "fid": "1",
                        "filename": "a.torrent",
                        "name": "",
                        "size": 100,
                        "seeders": 1,
                        "leechers": 0
                    },
                    {
                        "fid": "2",
                        "filename": "b.torrent",
                        "name": "Valid Name",
                        "size": 200,
                        "seeders": 2,
                        "leechers": 1
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Single(results);
        Assert.Equal("Valid Name", results[0].Title);
    }

    [Fact]
    public void ParseSearchResponse_WithStringNumericValues_ParsesCorrectly()
    {
        // Some JSON responses may have numeric values as strings
        var json = """
            {
                "numFound": 1,
                "torrentList": [
                    {
                        "fid": "500",
                        "filename": "test.torrent",
                        "name": "String Numbers Test",
                        "size": 999999,
                        "seeders": 42,
                        "leechers": 7,
                        "categoryID": 14
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, TestIndexer);

        Assert.Single(results);
        Assert.Equal(999999L, results[0].SizeBytes);
        Assert.Equal(42, results[0].Seeders);
        Assert.Equal(7, results[0].Leechers);
        Assert.Equal("14", results[0].Category);
    }

    [Fact]
    public void ParseSearchResponse_UrlGeneration_WithTrailingSlash()
    {
        var indexerWithSlash = new IndexerDefinition
        {
            Id = Guid.NewGuid(),
            Name = "TL",
            Type = IndexerType.TorrentLeech,
            BaseUrl = "https://www.torrentleech.org/",
            AuthMode = AuthMode.Cookie,
            Username = "user",
            Password = "pass"
        };

        var json = """
            {
                "numFound": 1,
                "torrentList": [
                    {
                        "fid": "12345",
                        "filename": "My.File.torrent",
                        "name": "My File",
                        "size": 100
                    }
                ]
            }
            """;

        var results = TorrentLeechClient.ParseSearchResponse(json, indexerWithSlash);

        Assert.Single(results);
        Assert.Equal("https://www.torrentleech.org/download/12345/My.File.torrent", results[0].DownloadUrl);
        Assert.Equal("https://www.torrentleech.org/torrent/12345", results[0].DetailsUrl);
    }
}

public class EngineDispatchTests
{
    [Fact]
    public async Task Search_DispatchesToCorrectClientByType()
    {
        var repo = new StubIndexerRepository();
        var torznabIndexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "Torznab Indexer", Type = IndexerType.Torznab,
            BaseUrl = "https://torznab.example.com", AuthMode = AuthMode.ApiKey, Enabled = true
        };
        var tlIndexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "TL Indexer", Type = IndexerType.TorrentLeech,
            BaseUrl = "https://www.torrentleech.org", AuthMode = AuthMode.Cookie, Enabled = true
        };
        repo.Seed(torznabIndexer, tlIndexer);

        var torznabClient = new StubIndexerClient(IndexerType.Torznab);
        torznabClient.SetResults(new TorrentSearchResult { Title = "Torznab Result", SizeBytes = 100 });

        var tlClient = new StubIndexerClient(IndexerType.TorrentLeech);
        tlClient.SetResults(new TorrentSearchResult { Title = "TL Result", SizeBytes = 200 });

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [torznabClient, tlClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Result.Title == "Torznab Result");
        Assert.Contains(results, r => r.Result.Title == "TL Result");
    }

    [Fact]
    public async Task Search_WithUnsupportedType_SkipsIndexer()
    {
        var repo = new StubIndexerRepository();
        // Create an indexer with TorrentLeech type but only register Torznab client
        var indexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "Unsupported", Type = IndexerType.TorrentLeech,
            BaseUrl = "https://example.com", AuthMode = AuthMode.Cookie, Enabled = true
        };
        repo.Seed(indexer);

        var torznabClient = new StubIndexerClient(IndexerType.Torznab);
        torznabClient.SetResults(new TorrentSearchResult { Title = "Should Not Appear" });

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [torznabClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_FailureInOneIndexer_DoesNotAffectOthers()
    {
        var repo = new StubIndexerRepository();
        var torznabIndexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "Good Indexer", Type = IndexerType.Torznab,
            BaseUrl = "https://good.example.com", AuthMode = AuthMode.ApiKey, Enabled = true
        };
        var tlIndexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "Bad Indexer", Type = IndexerType.TorrentLeech,
            BaseUrl = "https://bad.example.com", AuthMode = AuthMode.Cookie, Enabled = true
        };
        repo.Seed(torznabIndexer, tlIndexer);

        var goodClient = new StubIndexerClient(IndexerType.Torznab);
        goodClient.SetResults(new TorrentSearchResult { Title = "Good Result", SizeBytes = 100 });

        var badClient = new StubIndexerClient(IndexerType.TorrentLeech);
        badClient.SetFailOnSearch();

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [goodClient, badClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var results = await service.SearchAsync(new SearchRequest { Query = "test" });

        Assert.Single(results);
        Assert.Equal("Good Result", results[0].Result.Title);
    }

    [Fact]
    public async Task TestConnection_ForTorrentLeechType_DelegatesToCorrectClient()
    {
        var repo = new StubIndexerRepository();
        var indexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "TL", Type = IndexerType.TorrentLeech,
            BaseUrl = "https://www.torrentleech.org", AuthMode = AuthMode.Cookie, Enabled = true
        };
        repo.Seed(indexer);

        var torznabClient = new StubIndexerClient(IndexerType.Torznab);
        torznabClient.SetTestConnectionResult(false);

        var tlClient = new StubIndexerClient(IndexerType.TorrentLeech);
        tlClient.SetTestConnectionResult(true);

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [torznabClient, tlClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var result = await service.TestIndexerAsync(indexer.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task TestConnection_WithNoMatchingClient_ReturnsFalse()
    {
        var repo = new StubIndexerRepository();
        var indexer = new IndexerDefinition
        {
            Id = Guid.NewGuid(), Name = "TL", Type = IndexerType.TorrentLeech,
            BaseUrl = "https://www.torrentleech.org", AuthMode = AuthMode.Cookie, Enabled = true
        };
        repo.Seed(indexer);

        // Only register Torznab client, but indexer is TorrentLeech
        var torznabClient = new StubIndexerClient(IndexerType.Torznab);
        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [torznabClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var result = await service.TestIndexerAsync(indexer.Id);

        Assert.False(result);
    }
}

public class IndexerDownloadTests
{
    private static IndexerDefinition MakeTorrentLeechIndexer() => new()
    {
        Id = Guid.NewGuid(),
        Name = "TL",
        Type = IndexerType.TorrentLeech,
        BaseUrl = "https://www.torrentleech.org",
        AuthMode = AuthMode.Cookie,
        Username = "user",
        Password = "pass",
        Enabled = true
    };

    private static byte[] LoadTestTorrentBytes() =>
        File.ReadAllBytes("debian-12.0.0-amd64-netinst.iso.torrent");

    [Fact]
    public async Task DownloadTorrentFile_WithValidIndexer_ReturnsBytes()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeTorrentLeechIndexer();
        repo.Seed(indexer);

        var torrentBytes = LoadTestTorrentBytes();
        var client = new StubIndexerClient(IndexerType.TorrentLeech);
        client.SetDownloadBytes(torrentBytes);

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var result = await service.DownloadTorrentFileAsync(
            indexer.Id, "https://www.torrentleech.org/download/12345/test.torrent");

        Assert.Equal(torrentBytes.Length, result.Length);
        Assert.Equal(torrentBytes, result);
    }

    [Fact]
    public async Task DownloadTorrentFile_WithNonExistentIndexer_Throws()
    {
        var repo = new StubIndexerRepository();
        var client = new StubIndexerClient(IndexerType.TorrentLeech);
        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DownloadTorrentFileAsync(Guid.NewGuid(), "https://example.com/test.torrent"));
    }

    [Fact]
    public async Task DownloadTorrentFile_WithNoMatchingClient_Throws()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeTorrentLeechIndexer();
        repo.Seed(indexer);

        // Only register Torznab client, but indexer is TorrentLeech
        var torznabClient = new StubIndexerClient(IndexerType.Torznab);
        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [torznabClient], metadata,
            NullLogger<IndexerSearchService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DownloadTorrentFileAsync(indexer.Id, "https://www.torrentleech.org/download/12345/test.torrent"));
    }

    [Fact]
    public async Task DownloadTorrentFile_WhenAuthFails_ThrowsHttpRequestException()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeTorrentLeechIndexer();
        repo.Seed(indexer);

        var client = new StubIndexerClient(IndexerType.TorrentLeech);
        client.SetFailOnDownload();

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.DownloadTorrentFileAsync(indexer.Id, "https://www.torrentleech.org/download/12345/test.torrent"));
    }

    [Fact]
    public async Task DownloadTorrentFile_BytesAreParseable()
    {
        var repo = new StubIndexerRepository();
        var indexer = MakeTorrentLeechIndexer();
        repo.Seed(indexer);

        var torrentBytes = LoadTestTorrentBytes();
        var client = new StubIndexerClient(IndexerType.TorrentLeech);
        client.SetDownloadBytes(torrentBytes);

        var metadata = new StubMetadataProvider();
        var service = new IndexerSearchService(
            repo, [client], metadata,
            NullLogger<IndexerSearchService>.Instance);

        var bytes = await service.DownloadTorrentFileAsync(
            indexer.Id, "https://www.torrentleech.org/download/12345/debian.torrent");

        using var ms = new MemoryStream(bytes);
        var meta = TorrentMetadataParser.FromStream(ms);

        Assert.NotNull(meta);
        Assert.False(string.IsNullOrEmpty(meta.Name));
        Assert.False(string.IsNullOrEmpty(meta.InfoHash));
        Assert.True(meta.TotalSize > 0);
        Assert.NotEmpty(meta.Files);
    }
}

public class NonSeekableStreamTests
{
    [Fact]
    public void FromStream_WithNonSeekableStream_BuffersAndParses()
    {
        var torrentBytes = File.ReadAllBytes("debian-12.0.0-amd64-netinst.iso.torrent");
        using var nonSeekableStream = new NonSeekableMemoryStream(torrentBytes);

        var meta = TorrentMetadataParser.FromStream(nonSeekableStream);

        Assert.NotNull(meta);
        Assert.False(string.IsNullOrEmpty(meta.Name));
        Assert.False(string.IsNullOrEmpty(meta.InfoHash));
        Assert.True(meta.TotalSize > 0);
    }

    [Fact]
    public void FromStream_WithSeekableStream_ParsesDirectly()
    {
        var torrentBytes = File.ReadAllBytes("debian-12.0.0-amd64-netinst.iso.torrent");
        using var seekableStream = new MemoryStream(torrentBytes);

        var meta = TorrentMetadataParser.FromStream(seekableStream);

        Assert.NotNull(meta);
        Assert.False(string.IsNullOrEmpty(meta.Name));
        Assert.True(meta.TotalSize > 0);
    }

    [Fact]
    public void FromStream_BothStreamTypes_ProduceSameResult()
    {
        var torrentBytes = File.ReadAllBytes("debian-12.0.0-amd64-netinst.iso.torrent");

        using var seekableStream = new MemoryStream(torrentBytes);
        var seekableResult = TorrentMetadataParser.FromStream(seekableStream);

        using var nonSeekableStream = new NonSeekableMemoryStream(torrentBytes);
        var nonSeekableResult = TorrentMetadataParser.FromStream(nonSeekableStream);

        Assert.Equal(seekableResult.Name, nonSeekableResult.Name);
        Assert.Equal(seekableResult.InfoHash, nonSeekableResult.InfoHash);
        Assert.Equal(seekableResult.TotalSize, nonSeekableResult.TotalSize);
        Assert.Equal(seekableResult.Files.Count, nonSeekableResult.Files.Count);
    }

    /// <summary>
    /// A wrapper stream that hides the CanSeek capability to simulate network streams.
    /// </summary>
    private class NonSeekableMemoryStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableMemoryStream(byte[] data)
        {
            _inner = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}

public class AddFromUrlRequestContractTests
{
    [Fact]
    public void AddFromUrlRequest_accepts_indexer_id()
    {
        var indexerId = Guid.NewGuid();
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://www.torrentleech.org/download/12345/test.torrent",
            IndexerId = indexerId,
            SelectedFileIds = new long[] { 0, 1 },
            CompletedPath = "/data/downloads"
        };

        Assert.Equal(indexerId, request.IndexerId);
        Assert.Equal("https://www.torrentleech.org/download/12345/test.torrent", request.Url);
    }

    [Fact]
    public void AddFromUrlRequest_indexer_id_is_optional()
    {
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://example.com/torrent.torrent"
        };

        Assert.Null(request.IndexerId);
    }

    [Fact]
    public void AddFromUrlRequest_backwards_compatible_with_all_fields()
    {
        var indexerId = Guid.NewGuid();
        var request = new TorrentAddFromUrlRequest
        {
            Url = "https://tracker.example.com/download/12345",
            IndexerId = indexerId,
            SelectedFileIds = new long[] { 1, 3 },
            CompletedPath = "/data/completed"
        };

        Assert.Equal("https://tracker.example.com/download/12345", request.Url);
        Assert.Equal(indexerId, request.IndexerId);
        Assert.NotNull(request.SelectedFileIds);
        Assert.Equal(2, request.SelectedFileIds.Length);
        Assert.Equal("/data/completed", request.CompletedPath);
    }
}
