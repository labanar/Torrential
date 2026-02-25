using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Clients;

public interface IIndexerClient
{
    IndexerType SupportedType { get; }
    Task<IReadOnlyList<TorrentSearchResult>> SearchAsync(IndexerDefinition indexer, SearchRequest request, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(IndexerDefinition indexer, CancellationToken ct = default);
}
