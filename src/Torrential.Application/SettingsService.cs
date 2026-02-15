using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Application.Persistence;

namespace Torrential.Application;

public class SettingsService(IServiceScopeFactory scopeFactory) : ISettingsService
{
    private TorrentDbContext CreateDbContext()
    {
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
    }

    public async Task<SettingsEntity> GetSettingsAsync()
    {
        using var db = CreateDbContext();
        var settings = await db.Settings.FindAsync(1);
        if (settings is not null) return settings;

        settings = new SettingsEntity();
        db.Settings.Add(settings);
        await db.SaveChangesAsync();
        return settings;
    }

    public async Task<SettingsEntity> UpdateSettingsAsync(string downloadFolder, string completedFolder, int maxHalfOpenConnections, int maxPeersPerTorrent)
    {
        using var db = CreateDbContext();
        var settings = await db.Settings.FindAsync(1);
        if (settings is null)
        {
            settings = new SettingsEntity();
            db.Settings.Add(settings);
        }

        settings.DownloadFolder = downloadFolder;
        settings.CompletedFolder = completedFolder;
        settings.MaxHalfOpenConnections = maxHalfOpenConnections;
        settings.MaxPeersPerTorrent = maxPeersPerTorrent;

        await db.SaveChangesAsync();
        return settings;
    }
}
