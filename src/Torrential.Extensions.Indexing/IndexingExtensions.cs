using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Extensions.Indexing.Clients;
using Torrential.Extensions.Indexing.Metadata;
using Torrential.Extensions.Indexing.Persistence;
using Torrential.Extensions.Indexing.Services;

namespace Torrential.Extensions.Indexing;

public static class IndexingExtensions
{
    public static IServiceCollection AddTorrentialIndexing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("Indexer");

        services.AddSingleton<IIndexerRepository, IndexerRepository>();
        services.AddSingleton<IIndexerClient, TorznabClient>();
        services.AddSingleton<IIndexerClient, RssClient>();
        services.AddSingleton<IIndexerSearchService, IndexerSearchService>();

        // Register the no-op metadata provider as the default.
        // Consumers can replace this registration with their own IMetadataProvider.
        services.AddSingleton<IMetadataProvider, NoOpMetadataProvider>();

        return services;
    }

    public static IEndpointRouteBuilder MapTorrentialIndexingEndpoints(this IEndpointRouteBuilder app)
    {
        IndexingEndpoints.Map(app);
        return app;
    }
}
