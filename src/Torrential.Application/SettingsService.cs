using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Application.Persistence;

namespace Torrential.Application;

public class SettingsService(IServiceScopeFactory scopeFactory) : ISettingsService
{
    private volatile SettingsEntity? _cached;
    private readonly object _lock = new();

    private TorrentDbContext CreateDbContext()
    {
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
    }

    public async Task<SettingsEntity> GetSettingsAsync()
    {
        var cached = _cached;
        if (cached is not null) return cached;

        using var db = CreateDbContext();
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1);
        if (settings is null)
        {
            settings = new SettingsEntity();
            db.Settings.Add(settings);
            await db.SaveChangesAsync();
        }

        lock (_lock)
        {
            _cached ??= settings;
            return _cached;
        }
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

        lock (_lock)
        {
            _cached = new SettingsEntity
            {
                Id = settings.Id,
                DownloadFolder = settings.DownloadFolder,
                CompletedFolder = settings.CompletedFolder,
                MaxHalfOpenConnections = settings.MaxHalfOpenConnections,
                MaxPeersPerTorrent = settings.MaxPeersPerTorrent
            };
        }

        return settings;
    }
}
