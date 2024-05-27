using MassTransit;
using MassTransit.Initializers;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
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
using Torrential.Web.Api.Responses.Torrents;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddOpenTelemetry()
    .WithMetrics(b =>
    {
        b.AddAspNetCoreInstrumentation()
         .AddMeter(PeerMetrics.MeterName)
         .AddRuntimeInstrumentation()
         .AddPrometheusExporter();
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins("http://localhost:3000", "http://localhost:5142"));
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
builder.Services.AddSingleton<InitializationService>();

var app = builder.Build();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.UseOpenTelemetryPrometheusScrapingEndpoint();
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
    TorrentStatusCache statusCache,
    TorrentStats rates,
    TorrentMetadataCache metaCache) =>
    {
        var summaries = new List<TorrentSummaryVm>();
        await foreach (var torrent in torrentDb.Torrents.AsAsyncEnumerable())
        {
            if (!metaCache.TryGet(torrent.InfoHash, out var meta)) continue;
            var peers = await swarms.GetPeers(torrent.InfoHash);
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

            if (!bitfieldManager.TryGetDownloadBitfield(torrent.InfoHash, out var downloadBitfield))
                continue;

            var status = await statusCache.GetStatus(torrent.InfoHash);
            var summary = new TorrentSummaryVm
            {
                Name = meta.Name,
                InfoHash = meta.InfoHash,
                Status = status.ToString(),
                Progress = downloadBitfield.CompletionRatio,
                TotalSizeBytes = meta.PieceSize * meta.NumberOfPieces,
                DownloadRate = rates.GetIngressRate(torrent.InfoHash),
                UploadRate = rates.GetEgressRate(torrent.InfoHash),
                BytesDownloaded = rates.GetTotalDownloaded(torrent.InfoHash),
                BytesUploaded = rates.GetTotalUploaded(torrent.InfoHash),
                Peers = peerSummaries,
            };

            summaries.Add(summary);
        }
        return new TorrentListResponse(summaries.ToArray());
    });

app.MapGet(
    "/torrents/{infoHash}",
    async (InfoHash infoHash, TorrentMetadataCache cache, PeerSwarm swarms, BitfieldManager bitfieldManager, TorrentStatusCache statusCache, TorrentStats rates) =>
    {
        if (!cache.TryGet(infoHash, out var meta))
            return TorrentGetResponse.ErrorResponse(ErrorCode.Unknown);

        if (!bitfieldManager.TryGetVerificationBitfield(infoHash, out var verificationBitfield))
            return TorrentGetResponse.ErrorResponse(ErrorCode.Unknown);

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

        var status = await statusCache.GetStatus(infoHash);
        var summary = new TorrentSummaryVm
        {
            Name = meta.Name,
            InfoHash = meta.InfoHash,
            Status = status.ToString(),
            Progress = verificationBitfield.CompletionRatio,
            TotalSizeBytes = meta.PieceSize * meta.NumberOfPieces,
            DownloadRate = rates.GetIngressRate(infoHash),
            UploadRate = rates.GetEgressRate(infoHash),
            BytesDownloaded = rates.GetTotalDownloaded(infoHash),
            BytesUploaded = rates.GetTotalUploaded(infoHash),
            Peers = peerSummaries,
        };


        return new TorrentGetResponse(summary);
    });

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
    async (InfoHash infoHash, TorrentRemoveCommand cmd, ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse> handler) =>
    {
        cmd.InfoHash = infoHash;
        await handler.Execute(cmd);
    });


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

app.MapGet("settings/connection", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetConnectionSettings();
    return new ConnectionSettingsGetResponse(settings);
});

app.MapPost("settings/connection", async (ConnectionSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveConnectionSettings(new()
    {
        MaxConnectionsPerTorrent = request.MaxConnectionsPerTorrent,
        MaxConnectionsGlobal = request.MaxConnectionsGlobal,
        MaxHalfOpenConnections = request.MaxHalfOpenConnections
    });
    return ActionResponse.SuccessResponse;
});


var initService = app.Services.GetRequiredService<InitializationService>();
await initService.Initialize(CancellationToken.None);

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

internal class InitializationService(
    IServiceProvider serviceProvider,
    TorrentTaskManager taskManager,
    IMetadataFileService metaFileService,
    TorrentStatusCache statusCache,
    IServiceScopeFactory scopeFactory,
    ILogger<InitializationService> logger)
{
    public async Task Initialize(CancellationToken stoppingToken)
    {
        await MigrateDatabase();
        logger.LogInformation("Migrated database");
        await LoadTorrents();
    }

    private async Task MigrateDatabase()
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
        await db.Database.MigrateAsync();
    }

    private async Task LoadTorrents()
    {
        using var scope = serviceProvider.CreateScope();
        await foreach (var torrentMeta in metaFileService.GetAllMetadataFiles())
        {
            await taskManager.Add(torrentMeta);
            var config = await GetFromDatabase(torrentMeta.InfoHash);
            if (config?.Status != null)
                statusCache.UpdateStatus(torrentMeta.InfoHash, config.Status);

            if (config?.Status == TorrentStatus.Running)
                await taskManager.Start(torrentMeta.InfoHash);
        }
    }

    private async Task<TorrentConfiguration?> GetFromDatabase(InfoHash infoHash)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
        var torrent = await db.Torrents.AsNoTracking().FirstOrDefaultAsync(x => x.InfoHash == infoHash.AsString());
        return torrent;
    }
}
