﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Commands;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Pipelines;
using Torrential.Settings;
using Torrential.Torrents;
using Torrential.Trackers;
using Torrential.Trackers.Http;
using Torrential.Trackers.Udp;

namespace Torrential;

public static class ServiceCollectionExtensions
{
    public static void AddTorrential(this IServiceCollection services)
    {
        var appDataPath = Environment.GetEnvironmentVariable("APP_DATA_PATH");
        var dbPath = Path.Combine(appDataPath ?? "", "torrential.db");

        services.AddDbContext<TorrentialDb>(config => config.UseSqlite($"Data Source={dbPath}"));
        services.AddSingleton<IPeerService, PeerService>();
        services.AddHttpClient<HttpTrackerClient>();
        services.AddSingleton<ITrackerClient>(sp => sp.GetRequiredService<HttpTrackerClient>());
        services.AddSingleton<ITrackerClient, UdpTrackerClient>();

        services.AddSingleton<IFileHandleProvider, FileHandleProvider>();
        services.AddSingleton<IMetadataFileService, MetadataFileService>();
        services.AddSingleton<IBlockSaveService, BlockSaveService>();
        services.AddSingleton<PeerSwarm>();
        services.AddSingleton<TorrentRunner>();
        services.AddSingleton<TorrentTaskManager>();

        services.AddSingleton<TorrentFileService>();
        services.AddSingleton<PieceReservationService>();
        services.AddSingleton<BitfieldManager>();
        services.AddSingleton<TorrentMetadataCache>();
        services.AddSingleton<PieceSelector>();
        services.AddSingleton<HandshakeService>();
        services.AddSingleton<SettingsManager>();
        services.AddSingleton<AnnounceServiceState>();
        services.AddSingleton<TorrentStatusCache>();
        services.AddSingleton<TorrentStats>();
        services.AddSingleton<PeerConnectionManager>();
        services.AddSingleton<GeoIpService>();


        //TODO - add service extension that scans the provided assemblies for implementations of ICommandHandler<,>
        services.AddCommandHandler<TorrentAddCommand, TorrentAddResponse, TorrentAddCommandHandler>();
        services.AddCommandHandler<TorrentStartCommand, TorrentStartResponse, TorrentStartCommandHandler>();
        services.AddCommandHandler<TorrentStopCommand, TorrentStopResponse, TorrentStopCommandHandler>();
        services.AddCommandHandler<TorrentRemoveCommand, TorrentRemoveResponse, TorrentRemoveCommandHandler>();

        services.AddHostedService<HalfOpenConnectionShakerService>();

        services.AddHostedService<TorrentThroughputRatesNotifier>();
        services.AddHostedService<BitfieldSyncService>();
        services.AddHostedService<AnnounceService>();
        services.AddHostedService<TcpPeerListenerBackgroundService>();

        services.AddSingleton<IPostDownloadAction, FileCopyPostDownloadAction>();
    }
}
