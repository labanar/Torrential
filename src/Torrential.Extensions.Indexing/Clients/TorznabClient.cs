using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Clients;

internal sealed class TorznabClient(IHttpClientFactory httpClientFactory, ILogger<TorznabClient> logger) : IIndexerClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public IndexerType SupportedType => IndexerType.Torznab;

    public async Task<IReadOnlyList<TorrentSearchResult>> SearchAsync(IndexerDefinition indexer, SearchRequest request, CancellationToken ct = default)
    {
        var url = BuildSearchUrl(indexer, request);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var client = CreateHttpClient(indexer);
            var response = await client.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            return ParseTorznabResponse(content, indexer);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Search request to indexer {IndexerName} timed out", indexer.Name);
            return [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP error searching indexer {IndexerName}", indexer.Name);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error searching indexer {IndexerName}", indexer.Name);
            return [];
        }
    }

    public async Task<bool> TestConnectionAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        var url = BuildCapabilitiesUrl(indexer);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var client = CreateHttpClient(indexer);
            var response = await client.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for indexer {IndexerName}", indexer.Name);
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

    private static string BuildSearchUrl(IndexerDefinition indexer, SearchRequest request)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api?t=search&q={Uri.EscapeDataString(request.Query)}";

        if (!string.IsNullOrEmpty(indexer.ApiKey))
            url += $"&apikey={Uri.EscapeDataString(indexer.ApiKey)}";

        if (!string.IsNullOrEmpty(request.Category))
            url += $"&cat={Uri.EscapeDataString(request.Category)}";

        if (request.Limit.HasValue)
            url += $"&limit={request.Limit.Value}";

        return url;
    }

    private static string BuildCapabilitiesUrl(IndexerDefinition indexer)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api?t=caps";

        if (!string.IsNullOrEmpty(indexer.ApiKey))
            url += $"&apikey={Uri.EscapeDataString(indexer.ApiKey)}";

        return url;
    }

    internal static IReadOnlyList<TorrentSearchResult> ParseTorznabResponse(string xml, IndexerDefinition indexer)
    {
        var results = new List<TorrentSearchResult>();

        try
        {
            var doc = XDocument.Parse(xml);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var torznabNs = XNamespace.Get("http://torznab.com/schemas/2015/feed");
                var result = new TorrentSearchResult
                {
                    Title = item.Element("title")?.Value ?? string.Empty,
                    DownloadUrl = item.Element("link")?.Value,
                    DetailsUrl = item.Element("guid")?.Value,
                    IndexerId = indexer.Id,
                    IndexerName = indexer.Name
                };

                var sizeAttr = item.Descendants(torznabNs + "attr")
                    .FirstOrDefault(a => a.Attribute("name")?.Value == "size");
                if (sizeAttr != null && long.TryParse(sizeAttr.Attribute("value")?.Value, out var size))
                    result.SizeBytes = size;

                // Fallback: check enclosure length
                if (result.SizeBytes == 0)
                {
                    var enclosure = item.Element("enclosure");
                    if (enclosure != null && long.TryParse(enclosure.Attribute("length")?.Value, out var enclosureSize))
                        result.SizeBytes = enclosureSize;
                    if (result.DownloadUrl is null)
                        result.DownloadUrl = enclosure?.Attribute("url")?.Value;
                }

                var seedersAttr = item.Descendants(torznabNs + "attr")
                    .FirstOrDefault(a => a.Attribute("name")?.Value == "seeders");
                if (seedersAttr != null && int.TryParse(seedersAttr.Attribute("value")?.Value, out var seeders))
                    result.Seeders = seeders;

                var leechersAttr = item.Descendants(torznabNs + "attr")
                    .FirstOrDefault(a => a.Attribute("name")?.Value == "peers");
                if (leechersAttr != null && int.TryParse(leechersAttr.Attribute("value")?.Value, out var peers))
                    result.Leechers = Math.Max(0, peers - result.Seeders);

                var infoHashAttr = item.Descendants(torznabNs + "attr")
                    .FirstOrDefault(a => a.Attribute("name")?.Value == "infohash");
                if (infoHashAttr != null)
                    result.InfoHash = infoHashAttr.Attribute("value")?.Value;

                var categoryAttr = item.Descendants(torznabNs + "attr")
                    .FirstOrDefault(a => a.Attribute("name")?.Value == "category");
                if (categoryAttr != null)
                    result.Category = categoryAttr.Attribute("value")?.Value;
                else
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
