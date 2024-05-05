using MassTransit;
using MassTransit.Initializers;
using Microsoft.AspNetCore.Connections;
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
using Torrential.Torrents;
using Torrential.Web.Api.Models;
using Torrential.Web.Api.Responses;

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
builder.Services.AddHostedService<InitializationService>();
builder.Services.AddConnections();
//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.ListenAnyIP(53123, listenOptions =>
//    {
//        listenOptions.UseConnectionHandler<TcpConnectionHandler>();
//    });

//    options.ListenLocalhost(5142);
//});

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

app.MapPost("settings",
       async (FileSettingsUpdateCommand command, ICommandHandler<FileSettingsUpdateCommand, FileSettingsUpdateResponse> handler) =>
              await handler.Execute(command));



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

internal class TcpConnectionHandler : ConnectionHandler
{
    private readonly ILogger<TcpConnectionHandler> _logger;
    private readonly HandshakeService _handshakeService;
    private readonly PeerSwarm _swarm;

    public TcpConnectionHandler(HandshakeService handshakeService, ILogger<TcpConnectionHandler> logger, PeerSwarm swarm)
    {
        _logger = logger;
        _handshakeService = handshakeService;
        _swarm = swarm;
    }

    public override async Task OnConnectedAsync(ConnectionContext connection)
    {
        var conn = new PeerWireDuplexPipeConnection(connection, _handshakeService, _logger);
        var result = await conn.ConnectInbound(CancellationToken.None);
        if (!result.Success)
        {
            _logger.LogWarning("Connection failed");
            return;
        }

        await _swarm.AddToSwarm(conn);
    }

    private static bool IsIPv4MappedToIPv6(byte[] bytes)
    {
        for (int i = 0; i < 10; i++)
        {
            if (bytes[i] != 0) return false;
        }
        if (bytes[10] != 0xff || bytes[11] != 0xff)
        {
            return false;
        }
        return true;
    }
}

internal class InitializationService(IServiceProvider serviceProvider, IMemoryCache cache, TorrentTaskManager taskManager, IMetadataFileService metaFileService, TcpPeerListener tcpPeerListener) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MigrateDatabase();
        await LoadSettings();
        await LoadTorrents();
        Task.Run(() => tcpPeerListener.Start(CancellationToken.None));
    }

    private async Task MigrateDatabase()
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
        await db.Database.MigrateAsync();
    }

    private async Task LoadSettings()
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
        var settings = await db.Settings.FindAsync(TorrentialSettings.DefaultId);
        if (settings == null)
        {
            var appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            settings = new()
            {
                FileSettings = new FileSettings
                {
                    DownloadPath = Path.Combine(appPath, "torrential\\downloads"),
                    CompletedPath = Path.Combine(appPath, "torrential\\completed")
                }
            };
            await db.Settings.AddAsync(settings);
            await db.SaveChangesAsync();
        }

        cache.Set("settings.file", settings.FileSettings);
    }

    private async Task LoadTorrents()
    {
        using var scope = serviceProvider.CreateScope();
        await foreach (var torrentMeta in metaFileService.GetAllMetadataFiles())
            await taskManager.Add(torrentMeta);
    }
}
