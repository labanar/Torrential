using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Application.Persistence;
using Torrential.Core;

namespace Torrential.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTorrentApplication(this IServiceCollection services)
    {
        var dataDir = Environment.GetEnvironmentVariable("APP_DATA_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "torrential");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "torrential-app.db");
        services.AddDbContext<TorrentDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<ITorrentManager, TorrentManager>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddHostedService<TorrentStoreInitializer>();
        services.AddSingleton<IInboundConnectionHandler, InboundConnectionHandler>();
        services.AddHostedService<TcpListenerService>();
        services.AddSingleton<IAnnounceService, AnnounceService>();
        services.AddSingleton<IPeerPool, PeerPool>();
        services.AddHostedService<TrackerAnnounceService>();
        services.AddSingleton<PeerConnectionService>();
        services.AddSingleton<IPeerConnectionManager>(sp => sp.GetRequiredService<PeerConnectionService>());
        services.AddHostedService(sp => sp.GetRequiredService<PeerConnectionService>());
        services.AddSingleton<IPieceStorage, DiskPieceStorage>();
        services.AddHostedService<PieceDownloadService>();
        return services;
    }
}
