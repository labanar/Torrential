using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Torrential", "indexing.db");

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            Directory.CreateDirectory(dbDirectory);

        services.AddDbContext<IndexingDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddHttpClient("Indexer");

        services.AddSingleton<IIndexerRepository, IndexerRepository>();
        services.AddSingleton<IIndexerClient, TorznabClient>();
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

    /// <summary>
    /// Ensures the indexing database is created and migrated.
    /// Call this during application startup.
    /// </summary>
    public static async Task InitializeIndexingAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
