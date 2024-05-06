using MassTransit;
using MassTransit.Initializers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Commands;
using Torrential.Extensions.SignalR;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Settings;
using Torrential.Torrents;
using Torrential.Web.Api.Models;
using Torrential.Web.Api.Requests.Settings;
using Torrential.Web.Api.Responses;
using Torrential.Web.Api.Responses.Settings;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin());
});

builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(TorrentHubMessageDispatcher).Assembly, typeof(PieceValidator).Assembly);
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddHostedService<InitializationService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapHub<TorrentHub>("/torrents/hub");


app.MapPost(
    "/torrents/add",
    async (IFormFile file, ICommandHandler<TorrentAddCommand, TorrentAddResponse> handler) =>
    {
        var meta = TorrentMetadataParser.FromStream(file.OpenReadStream());
        return await handler.Execute(new() { Metadata = meta, DownloadPath = "", CompletedPath = "" });
    })
    .DisableAntiforgery();

app.MapGet(
    "/torrents",
    async (PeerSwarm swarms,
    TorrentialDb torrentDb,
    BitfieldManager bitfieldManager,
    TorrentMetadataCache metaCache) =>
    {
        var summaries = new List<TorrentSummaryVm>();
        await foreach (var torrents in torrentDb.Torrents.AsAsyncEnumerable())
        {
            if (!metaCache.TryGet(torrents.InfoHash, out var meta)) continue;
            var peers = await swarms.GetPeers(torrents.InfoHash);
            var peerSummaries = peers.Select(x => new PeerSummaryVm
            {
                PeerId = x.PeerId.ToAsciiString(),
                IpAddress = x.PeerInfo.Ip.ToString(),
                Port = x.PeerInfo.Port,
                BytesDownloaded = x.BytesDownloaded,
                BytesUploaded = x.BytesUploaded,
                IsSeed = x.State?.PeerBitfield?.HasAll() ?? false,
                Progress = x.State?.PeerBitfield?.CompletionRatio ?? 0
            });

            if (!bitfieldManager.TryGetDownloadBitfield(torrents.InfoHash, out var downloadBitfield))
                continue;

            var summary = new TorrentSummaryVm
            {
                Name = meta.Name,
                InfoHash = meta.InfoHash,
                Progress = downloadBitfield.CompletionRatio,
                Peers = peerSummaries,
            };

            summaries.Add(summary);
        }
        return Results.Json(summaries);
    })
    .Produces<IDataResponse<TorrentSummaryVm[]>>(200)
    .Produces<IErrorResponse>(400)
    .Produces<IErrorResponse>(500);

app.MapGet(
    "/torrents/{infoHash}",
    async (InfoHash infoHash, TorrentMetadataCache cache, PeerSwarm swarms, BitfieldManager bitfieldManager) =>
    {
        if (!cache.TryGet(infoHash, out var meta))
            return Results.NotFound();

        if (!bitfieldManager.TryGetVerificationBitfield(infoHash, out var verificationBitfield))
            return Results.NotFound();

        var peers = await swarms.GetPeers(infoHash);
        var peerSummaries = peers.Select(x => new PeerSummaryVm
        {
            PeerId = x.PeerId.ToAsciiString(),
            IpAddress = x.PeerInfo.Ip.ToString(),
            Port = x.PeerInfo.Port,
            BytesDownloaded = x.BytesDownloaded,
            BytesUploaded = x.BytesUploaded,
            IsSeed = x.State?.PeerBitfield?.HasAll() ?? false,
            Progress = x.State?.PeerBitfield?.CompletionRatio ?? 0
        });


        var summary = new TorrentSummaryVm
        {
            Name = meta.Name,
            InfoHash = meta.InfoHash,
            Progress = verificationBitfield.CompletionRatio,
            Peers = peerSummaries,
        };

        return Results.Json(summary);
    })
    .Produces<IDataResponse<TorrentSummaryVm>>(200)
    .Produces<IErrorResponse>(404)
    .Produces<IErrorResponse>(400)
    .Produces<IErrorResponse>(500);

app.MapPost(
    "torrents/{infoHash}/start",
    async (InfoHash infoHash, ICommandHandler<TorrentStartCommand, TorrentStartResponse> handler) =>
        await handler.Execute(new() { InfoHash = infoHash }));

app.MapPost(
    "torrents/{infoHash}/stop",
    async (InfoHash infoHash, ICommandHandler<TorrentStopCommand, TorrentStopResponse> handler) =>
        await handler.Execute(new() { InfoHash = infoHash }));

app.MapPost(
    "torrents/{infoHash}/delete",
    async (InfoHash infoHash, ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse> handler) =>
        await handler.Execute(new() { InfoHash = infoHash }));


app.MapGet("settings/file", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetFileSettings();
    return new FileSettingsGetResponse(settings);
});
app.MapPost("settings/file", async (FileSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveFileSettings(new() { CompletedPath = request.CompletedPath, DownloadPath = request.DownloadPath });
    return ActionResponse.SuccessResponse;
});

app.MapGet("settings/tcp", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetTcpListenerSettings();
    return new TcpListenerSettingsGetResponse(settings);
});
app.MapPost("settings/tcp", async (TcpListenerSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveTcpListenerSettings(new() { Port = request.Port, Enabled = request.Enabled });
    return ActionResponse.SuccessResponse;
});

app.MapGet("settings/torrent/default", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetDefaultTorrentSettings();
    return new DefaultTorrentSettingsGetResponse(settings);
});
app.MapPost("settings/torrent/default", async (DefaultTorrentSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveDefaultTorrentSettings(new() { MaxConnections = request.MaxConnections });
    return ActionResponse.SuccessResponse;
});

app.MapGet("settings/torrent/global", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetGlobalTorrentSettings();
    return new GlobalTorrentSettingsGetResponse(settings);
});
app.MapPost("settings/torrent/global", async (GlobalTorrentSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveGlobalTorrentSettings(new() { MaxConnections = request.MaxConnections });
    return ActionResponse.SuccessResponse;
});


await app.RunAsync();


static Logger BuildLogger(IConfiguration configuration)
{
    var lokiUrl = configuration.GetValue<string>("GrafanaLokiUrl");
    var config = new LoggerConfiguration();
    config.WriteTo.Console();

    if (!string.IsNullOrEmpty(lokiUrl))
        config.WriteTo.GrafanaLoki(lokiUrl, new LokiLabel[]
        {
            new()
            {
                Key = "app_name",
                Value = "torrential"
            }
        });

    return config.CreateLogger();
}

internal class InitializationService(IServiceProvider serviceProvider, IMemoryCache cache, TorrentTaskManager taskManager, IMetadataFileService metaFileService, TcpPeerListener tcpPeerListener) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MigrateDatabase();
        await LoadTorrents();
        Task.Run(() => tcpPeerListener.Start(CancellationToken.None));
    }

    private async Task MigrateDatabase()
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
        await db.Database.MigrateAsync();
    }

    //private async Task LoadSettings()
    //{
    //    using var scope = serviceProvider.CreateScope();
    //    var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
    //    var settings = await db.Settings.FindAsync(TorrentialSettings.DefaultId);
    //    if (settings == null)
    //    {
    //        var appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    //        settings = new()
    //        {
    //            FileSettings = new PersistedFileSettings
    //            {
    //                DownloadPath = Path.Combine(appPath, "torrential\\downloads"),
    //                CompletedPath = Path.Combine(appPath, "torrential\\completed")
    //            }
    //        };
    //        await db.Settings.AddAsync(settings);
    //        await db.SaveChangesAsync();
    //    }

    //    cache.Set("settings.file", settings.FileSettings);
    //}

    private async Task LoadTorrents()
    {
        using var scope = serviceProvider.CreateScope();
        await foreach (var torrentMeta in metaFileService.GetAllMetadataFiles())
            await taskManager.Add(torrentMeta);
    }
}
