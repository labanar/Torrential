using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Metadata;

internal sealed class NoOpMetadataProvider : IMetadataProvider
{
    public Task<MetadataEnrichment?> EnrichAsync(TorrentSearchResult result, CancellationToken ct = default)
        => Task.FromResult<MetadataEnrichment?>(null);
}
