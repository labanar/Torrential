using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Torrential.Extensions.Indexing.Models;
using Torrential.Models;

namespace Torrential.Extensions.Indexing.Clients;

internal sealed class TorrentLeechClient(IHttpClientFactory httpClientFactory, ILogger<TorrentLeechClient> logger) : IIndexerClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public IndexerType SupportedType => IndexerType.TorrentLeech;

    public async Task<IReadOnlyList<TorrentSearchResult>> SearchAsync(IndexerDefinition indexer, SearchRequest request, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
            using var client = new HttpClient(handler) { Timeout = RequestTimeout };
            SetCommonHeaders(client);

            if (!await LoginAsync(client, indexer, cts.Token))
            {
                logger.LogWarning("Authentication failed for TorrentLeech indexer {IndexerName}", indexer.Name);
                return [];
            }

            var results = new List<TorrentSearchResult>();
            var page = 1;
            var limit = request.Limit ?? 35; // TorrentLeech returns ~35 per page

            while (results.Count < limit)
            {
                var url = BuildSearchUrl(indexer, request.Query, page);
                var response = await client.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Search request to TorrentLeech indexer {IndexerName} returned {StatusCode}", indexer.Name, response.StatusCode);
                    break;
                }

                var json = await response.Content.ReadAsStringAsync(cts.Token);
                var pageResults = ParseSearchResponse(json, indexer);

                if (pageResults.Count == 0)
                    break;

                results.AddRange(pageResults);
                page++;

                // If we got fewer results than a full page, there are no more pages
                if (pageResults.Count < 35)
                    break;
            }

            if (results.Count > limit)
                results = results.Take(limit).ToList();

            return results;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Search request to TorrentLeech indexer {IndexerName} timed out", indexer.Name);
            return [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP error searching TorrentLeech indexer {IndexerName}", indexer.Name);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error searching TorrentLeech indexer {IndexerName}", indexer.Name);
            return [];
        }
    }

    public async Task<bool> TestConnectionAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
            using var client = new HttpClient(handler) { Timeout = RequestTimeout };
            SetCommonHeaders(client);

            if (!await LoginAsync(client, indexer, cts.Token))
                return false;

            // Verify session by performing a minimal search
            var url = BuildSearchUrl(indexer, "test", 1);
            var response = await client.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for TorrentLeech indexer {IndexerName}", indexer.Name);
            return false;
        }
    }

    private async Task<bool> LoginAsync(HttpClient client, IndexerDefinition indexer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(indexer.Username) || string.IsNullOrEmpty(indexer.Password))
        {
            logger.LogWarning("TorrentLeech indexer {IndexerName} requires username and password", indexer.Name);
            return false;
        }

        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        var loginUrl = $"{baseUrl}/user/account/login/";

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = indexer.Username,
            ["password"] = indexer.Password
        });

        var loginResponse = await client.PostAsync(loginUrl, formContent, ct);

        // TorrentLeech may redirect on successful login; a 200 or 3xx is acceptable
        if (loginResponse.StatusCode == HttpStatusCode.Forbidden ||
            loginResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        return loginResponse.IsSuccessStatusCode ||
               (int)loginResponse.StatusCode >= 300 && (int)loginResponse.StatusCode < 400;
    }

    private static void SetCommonHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:148.0) Gecko/20100101 Firefox/148.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    private static string BuildSearchUrl(IndexerDefinition indexer, string query, int page)
    {
        var baseUrl = indexer.BaseUrl.TrimEnd('/');
        var encodedQuery = Uri.EscapeDataString(query);
        return $"{baseUrl}/torrents/browse/list/query/{encodedQuery}/page/{page}";
    }

    internal static IReadOnlyList<TorrentSearchResult> ParseSearchResponse(string json, IndexerDefinition indexer)
    {
        var results = new List<TorrentSearchResult>();

        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("torrentList", out var torrentList) || torrentList.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var item in torrentList.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var fid = item.TryGetProperty("fid", out var fidProp) ? fidProp.GetString() : null;
                var filename = item.TryGetProperty("filename", out var filenameProp) ? filenameProp.GetString() : null;

                var result = new TorrentSearchResult
                {
                    Title = name,
                    IndexerId = indexer.Id,
                    IndexerName = indexer.Name
                };

                // Size
                if (item.TryGetProperty("size", out var sizeProp))
                {
                    if (sizeProp.ValueKind == JsonValueKind.Number)
                        result.SizeBytes = sizeProp.GetInt64();
                    else if (sizeProp.ValueKind == JsonValueKind.String && long.TryParse(sizeProp.GetString(), out var sizeVal))
                        result.SizeBytes = sizeVal;
                }

                // Seeders
                if (item.TryGetProperty("seeders", out var seedersProp))
                {
                    if (seedersProp.ValueKind == JsonValueKind.Number)
                        result.Seeders = seedersProp.GetInt32();
                    else if (seedersProp.ValueKind == JsonValueKind.String && int.TryParse(seedersProp.GetString(), out var seedVal))
                        result.Seeders = seedVal;
                }

                // Leechers
                if (item.TryGetProperty("leechers", out var leechersProp))
                {
                    if (leechersProp.ValueKind == JsonValueKind.Number)
                        result.Leechers = leechersProp.GetInt32();
                    else if (leechersProp.ValueKind == JsonValueKind.String && int.TryParse(leechersProp.GetString(), out var leechVal))
                        result.Leechers = leechVal;
                }

                // Publish date
                if (item.TryGetProperty("addedTimestamp", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
                {
                    var dateStr = dateProp.GetString();
                    if (!string.IsNullOrEmpty(dateStr) &&
                        DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var pubDate))
                    {
                        result.PublishDate = pubDate;
                    }
                }

                // Category from categoryID
                if (item.TryGetProperty("categoryID", out var catProp))
                {
                    if (catProp.ValueKind == JsonValueKind.Number)
                        result.Category = catProp.GetInt32().ToString();
                    else if (catProp.ValueKind == JsonValueKind.String)
                        result.Category = catProp.GetString();
                }

                // Build download URL: /download/{fid}/{filename}
                if (!string.IsNullOrEmpty(fid) && !string.IsNullOrEmpty(filename))
                {
                    var baseUrl = indexer.BaseUrl.TrimEnd('/');
                    result.DownloadUrl = $"{baseUrl}/download/{fid}/{filename}";
                }

                // Build details URL: /torrent/{fid}
                if (!string.IsNullOrEmpty(fid))
                {
                    var baseUrl = indexer.BaseUrl.TrimEnd('/');
                    result.DetailsUrl = $"{baseUrl}/torrent/{fid}";
                }

                results.Add(result);
            }
        }
        catch (JsonException ex)
        {
            // Malformed JSON — return whatever we parsed so far
        }

        return results;
    }
}
