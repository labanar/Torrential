using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Application.BlockSaving;
using Torrential.Application.Data;
using Torrential.Application.EventHandlers;
using Torrential.Application.Events;
using Torrential.Application.FileCopy;
using Torrential.Application.Files;
using Torrential.Application.Peers;
using Torrential.Application.PieceSelection;
using Torrential.Application.Services;
using Torrential.Application.Settings;
using Torrential.Application.Torrents;
using Torrential.Application.Trackers;
using Torrential.Application.Trackers.Http;
using Torrential.Application.Trackers.Udp;
using Torrential.Application.Verification;

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

        // Strategy implementations
        services.AddScoped<IBlockSaveService, BlockSaveService>();
        services.AddScoped<IPieceVerifier, PieceVerifier>();
        services.AddScoped<IFileCopyService, FileCopyService>();
        services.AddScoped<IPieceSelector, PieceSelector>();

        // Event handlers
        services.AddScoped<IEventHandler<TorrentStartedEvent>, TorrentStatusEventHandler>();
        services.AddScoped<IEventHandler<TorrentStoppedEvent>, TorrentStatusEventHandler>();
        services.AddScoped<IEventHandler<TorrentRemovedEvent>, TorrentStatusEventHandler>();

        services.AddScoped<IEventHandler<TorrentBlockDownloaded>, TorrentStatsEventHandler>();
        services.AddScoped<IEventHandler<TorrentBlockUploadedEvent>, TorrentStatsEventHandler>();

        services.AddScoped<IEventHandler<PieceValidationRequest>, PieceVerificationEventHandler>();

        services.AddScoped<IEventHandler<TorrentCompleteEvent>, PostDownloadEventHandler>();

        services.AddScoped<IEventHandler<TorrentStartedEvent>, AnnounceServiceEventHandler>();
        services.AddScoped<IEventHandler<TorrentStoppedEvent>, AnnounceServiceEventHandler>();
        services.AddScoped<IEventHandler<TorrentRemovedEvent>, AnnounceServiceEventHandler>();

        services.AddScoped<IEventHandler<TorrentPieceVerifiedEvent>, PeerSwarmMessageDispatcher>();

        // Torrent management services
        services.AddScoped<TorrentApplicationService>();
        services.AddSingleton<TorrentTaskManager>();
        services.AddSingleton<TorrentRunner>();

        // Peer networking
        services.AddSingleton<IPeerSwarm, PeerSwarm>();
        services.AddSingleton<PeerConnectionManager>();
        services.AddSingleton<HandshakeService>();
        services.AddSingleton<PieceReservationService>();
        services.AddSingleton<BitfieldManager>();
        services.AddSingleton<AnnounceServiceState>();

        // Background services
        services.AddHostedService<BitfieldSyncService>();
        services.AddHostedService<AnnounceService>();
        services.AddHostedService<TcpPeerListenerBackgroundService>();
        services.AddHostedService<HalfOpenConnectionShakerService>();
        services.AddHostedService<TorrentThroughputRatesNotifier>();

        // Tracker clients
        services.AddHttpClient<HttpTrackerClient>();
        services.AddSingleton<ITrackerClient, UdpTrackerClient>();
        services.AddSingleton<ITrackerClient>(sp => sp.GetRequiredService<HttpTrackerClient>());
    }
}
