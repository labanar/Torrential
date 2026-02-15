using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Torrential.Application.Persistence;

public class TorrentStoreInitializer(
    IServiceScopeFactory scopeFactory,
    ITorrentManager torrentManager,
    ILogger<TorrentStoreInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var entities = await db.Torrents.ToListAsync(cancellationToken);
        if (entities.Count > 0)
        {
            ((TorrentManager)torrentManager).LoadFromEntities(entities);
            logger.LogInformation("Loaded {Count} torrents from database", entities.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
