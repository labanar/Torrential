using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Torrential.Commands;
using Torrential.Settings;

namespace Torrential.Torrents;

internal sealed class SeedTimeEnforcementService(
    IServiceScopeFactory scopeFactory,
    SettingsManager settingsManager,
    ILogger<SeedTimeEnforcementService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Seed time enforcement service started");

        // Delay initial check to let the system finish initializing
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        var timer = new PeriodicTimer(CheckInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnforceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during seed time enforcement check");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }

        logger.LogInformation("Seed time enforcement service stopped");
    }

    private async Task EnforceAsync(CancellationToken ct)
    {
        var globalSeedSettings = await settingsManager.GetSeedSettings();

        List<TorrentConfiguration> candidates;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            candidates = await db.Torrents
                .AsNoTracking()
                .Where(t => t.DateFirstSeeded != null)
                .ToListAsync(ct);
        }

        foreach (var torrent in candidates)
        {
            var seedTimeDays = torrent.DesiredSeedTimeDays ?? globalSeedSettings.DesiredSeedTimeDays;
            if (seedTimeDays <= 0)
                continue;

            var requiredSeconds = (long)seedTimeDays * 86400;
            if (torrent.TotalSeededSeconds < requiredSeconds)
                continue;

            var elapsedDays = torrent.TotalSeededSeconds / 86400.0;
            logger.LogInformation(
                "Torrent {InfoHash} has accumulated {ElapsedDays:F1} days of seeding time (limit: {LimitDays}), removing",
                torrent.InfoHash, elapsedDays, seedTimeDays);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider
                    .GetRequiredService<ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse>>();

                await handler.Execute(new TorrentRemoveCommand
                {
                    InfoHash = torrent.InfoHash,
                    DeleteFiles = false
                });

                logger.LogInformation("Successfully removed torrent {InfoHash} after seed time elapsed", torrent.InfoHash);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to remove torrent {InfoHash} after seed time elapsed", torrent.InfoHash);
            }
        }
    }
}
