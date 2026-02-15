using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Application.Data;
using Torrential.Application.Events;
using Torrential.Application.Files;
using Torrential.Application.Peers;
using Torrential.Application.Services;
using Torrential.Application.Settings;
using Torrential.Application.Torrents;

namespace Torrential.Application;

public static class ServiceCollectionExtensions
{
    public static void AddTorrentialApplication(this IServiceCollection services)
    {
        var dbPath = Path.Combine(FileUtilities.AppDataPath, "torrential.db");

        services.AddDbContext<TorrentialDb>(config => config.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IEventBus, InProcessEventBus>();
        services.AddSingleton<TorrentMetadataCache>();
        services.AddSingleton<TorrentStatusCache>();
        services.AddSingleton<TorrentStats>();
        services.AddSingleton<TorrentFileService>();
        services.AddSingleton<IFileHandleProvider, FileHandleProvider>();
        services.AddSingleton<IMetadataFileService, MetadataFileService>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<IPeerService, PeerService>();
        services.AddSingleton<GeoIpService>();
    }
}
