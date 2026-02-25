using Microsoft.Extensions.Logging;
using Torrential.Extensions.Indexing.Clients;
using Torrential.Extensions.Indexing.Metadata;
using Torrential.Extensions.Indexing.Models;
using Torrential.Extensions.Indexing.Persistence;

namespace Torrential.Extensions.Indexing.Services;

public interface IIndexerSearchService
{
    Task<IReadOnlyList<EnrichedSearchResult>> SearchAsync(SearchRequest request, CancellationToken ct = default);
    Task<bool> TestIndexerAsync(Guid indexerId, CancellationToken ct = default);
}

internal sealed class IndexerSearchService(
    IIndexerRepository repository,
    IEnumerable<IIndexerClient> clients,
    IMetadataProvider metadataProvider,
    ILogger<IndexerSearchService> logger) : IIndexerSearchService
{
    private static readonly TimeSpan PerIndexerTimeout = TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<EnrichedSearchResult>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        var enabledIndexers = await repository.GetEnabledAsync(ct);
        if (enabledIndexers.Count == 0)
        {
            logger.LogInformation("No enabled indexers configured, returning empty results");
            return [];
        }

        var searchTasks = enabledIndexers.Select(indexer => SearchSingleIndexer(indexer, request, ct));
        var allResults = await Task.WhenAll(searchTasks);
        var flatResults = allResults.SelectMany(r => r).ToList();

        logger.LogInformation("Aggregated {Count} results from {IndexerCount} indexers for query '{Query}'",
            flatResults.Count, enabledIndexers.Count, request.Query);

        var enrichmentTasks = flatResults.Select(result => EnrichResult(result, ct));
        var enriched = await Task.WhenAll(enrichmentTasks);

        return enriched;
    }

    public async Task<bool> TestIndexerAsync(Guid indexerId, CancellationToken ct = default)
    {
        var indexer = await repository.GetByIdAsync(indexerId, ct);
        if (indexer is null) return false;

        var client = GetClientForType(indexer.Type);
        if (client is null)
        {
            logger.LogWarning("No client available for indexer type {Type}", indexer.Type);
            return false;
        }

        return await client.TestConnectionAsync(indexer, ct);
    }

    private async Task<IReadOnlyList<TorrentSearchResult>> SearchSingleIndexer(
        IndexerDefinition indexer, SearchRequest request, CancellationToken ct)
    {
        var client = GetClientForType(indexer.Type);
        if (client is null)
        {
            logger.LogWarning("No client available for indexer type {Type}, skipping {IndexerName}", indexer.Type, indexer.Name);
            return [];
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerIndexerTimeout);

        try
        {
            return await client.SearchAsync(indexer, request, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Search timed out for indexer {IndexerName}", indexer.Name);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search indexer {IndexerName}", indexer.Name);
            return [];
        }
    }

    private async Task<EnrichedSearchResult> EnrichResult(TorrentSearchResult result, CancellationToken ct)
    {
        try
        {
            var metadata = await metadataProvider.EnrichAsync(result, ct);
            return new EnrichedSearchResult { Result = result, Metadata = metadata };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Metadata enrichment failed for '{Title}', returning unenriched result", result.Title);
            return new EnrichedSearchResult { Result = result };
        }
    }

    private IIndexerClient? GetClientForType(IndexerType type)
        => clients.FirstOrDefault(c => c.SupportedType == type);
}
