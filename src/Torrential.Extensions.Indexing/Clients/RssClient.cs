using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Torrential.Extensions.Indexing.Models;
using Torrential.Models;

namespace Torrential.Extensions.Indexing.Clients;

internal sealed class RssClient(IHttpClientFactory httpClientFactory, ILogger<RssClient> logger) : IIndexerClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public IndexerType SupportedType => IndexerType.Rss;

    public async Task<IReadOnlyList<TorrentSearchResult>> SearchAsync(IndexerDefinition indexer, SearchRequest request, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var client = CreateHttpClient(indexer);
            var response = await client.GetAsync(indexer.BaseUrl, cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var results = ParseRssResponse(content, indexer);

            // RSS feeds don't support server-side search, so filter client-side
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                results = results
                    .Where(r => r.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (request.Limit.HasValue)
                results = results.Take(request.Limit.Value).ToList();

            return results;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("RSS feed request to indexer {IndexerName} timed out", indexer.Name);
            return [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP error fetching RSS feed from indexer {IndexerName}", indexer.Name);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching RSS feed from indexer {IndexerName}", indexer.Name);
            return [];
        }
    }

    public async Task<byte[]> DownloadTorrentAsync(IndexerDefinition indexer, string downloadUrl, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        var client = CreateHttpClient(indexer);
        return await client.GetByteArrayAsync(downloadUrl, cts.Token);
    }

    public async Task<bool> TestConnectionAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var client = CreateHttpClient(indexer);
            var response = await client.GetAsync(indexer.BaseUrl, cts.Token);
            if (!response.IsSuccessStatusCode) return false;

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var doc = XDocument.Parse(content);
            return doc.Root?.Name.LocalName == "rss" || doc.Descendants("channel").Any();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for RSS indexer {IndexerName}", indexer.Name);
            return false;
        }
    }

    private HttpClient CreateHttpClient(IndexerDefinition indexer)
    {
        var client = httpClientFactory.CreateClient("Indexer");
        client.Timeout = RequestTimeout;

        if (indexer.AuthMode == AuthMode.BasicAuth
            && !string.IsNullOrEmpty(indexer.Username)
            && !string.IsNullOrEmpty(indexer.Password))
        {
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{indexer.Username}:{indexer.Password}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        }

        return client;
    }

    internal static List<TorrentSearchResult> ParseRssResponse(string xml, IndexerDefinition indexer)
    {
        var results = new List<TorrentSearchResult>();

        try
        {
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var result = new TorrentSearchResult
                {
                    Title = item.Element("title")?.Value ?? string.Empty,
                    DownloadUrl = item.Element("link")?.Value,
                    DetailsUrl = item.Element("guid")?.Value ?? item.Element("link")?.Value,
                    IndexerId = indexer.Id,
                    IndexerName = indexer.Name
                };

                // Try enclosure for download URL and size
                var enclosure = item.Element("enclosure");
                if (enclosure != null)
                {
                    var enclosureUrl = enclosure.Attribute("url")?.Value;
                    if (!string.IsNullOrEmpty(enclosureUrl))
                        result.DownloadUrl = enclosureUrl;

                    if (long.TryParse(enclosure.Attribute("length")?.Value, out var enclosureSize))
                        result.SizeBytes = enclosureSize;
                }

                // Try to extract category from <category> element
                result.Category = item.Element("category")?.Value;

                var pubDateStr = item.Element("pubDate")?.Value;
                if (!string.IsNullOrEmpty(pubDateStr) && DateTimeOffset.TryParse(pubDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var pubDate))
                    result.PublishDate = pubDate;

                if (!string.IsNullOrWhiteSpace(result.Title))
                    results.Add(result);
            }
        }
        catch (Exception)
        {
            // Malformed XML — return whatever we parsed so far
        }

        return results;
    }
}
