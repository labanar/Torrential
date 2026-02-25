using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Metadata;

public interface IMetadataProvider
{
    Task<MetadataEnrichment?> EnrichAsync(TorrentSearchResult result, CancellationToken ct = default);
}
