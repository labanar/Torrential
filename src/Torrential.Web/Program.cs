using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Commands;
using Torrential.Extensions.SignalR;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Pipelines;
using Torrential.Settings;
using Torrential.Torrents;
using Torrential.Trackers;
using Torrential.Web.Api.Models;
using Torrential.Web.Api.Models.Torrents;
using Torrential.Web.Api.Requests.Torrents;
using Torrential.Web.Api.Requests.Settings;
using Torrential.Web.Api.Requests.Torrents;
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
builder.Services.AddSingleton<PieceVerifiedBatchService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PieceVerifiedBatchService>());
builder.Services.AddSingleton<TorrentHubMessageDispatcher>();
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
            .WithOrigins("http://localhost:3000", "http://localhost:5142", "http://192.168.10.49:5142", "http://localhost:5173", "http://192.168.10.30:5142"));
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddSingleton<InitializationService>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Wire up TorrentEventBus handlers.
// All services are singletons so we resolve once and register handler delegates.
// This replaces all MassTransit IConsumer<T> registrations with zero-allocation
// direct method dispatch.
// ---------------------------------------------------------------------------
WireEventBus(app.Services);

app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapHub<TorrentHub>("/torrents/hub");


app.MapPost(
    "/torrents/preview",
    ([FromForm] TorrentPreviewRequest request) =>
    {
        try
        {
            var meta = TorrentMetadataParser.FromStream(request.File.OpenReadStream());
            var files = meta.Files.Select(x => new TorrentPreviewFileVm
            {
                Id = x.Id,
                Filename = x.Filename,
                SizeBytes = x.FileSize,
                DefaultSelected = true
            }).ToArray();

            return new TorrentPreviewResponse(new TorrentPreviewVm
            {
                Name = meta.Name,
                InfoHash = meta.InfoHash,
                TotalSizeBytes = meta.TotalSize,
                Files = files
            });
        }
        catch
        {
            return TorrentPreviewResponse.ErrorResponse(ErrorCode.Unknown);
        }
    })
    .DisableAntiforgery();

app.MapPost(
    "/torrents/add",
    ([FromForm] TorrentAddRequest request, IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory) =>
    {
        var meta = TorrentMetadataParser.FromStream(request.File.OpenReadStream());
        var selectedFileIds = request.SelectedFileIds;
        var logger = loggerFactory.CreateLogger("TorrentAddEndpoint");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<TorrentAddCommand, TorrentAddResponse>>();
                await handler.Execute(new()
                {
                  Metadata = meta,
                  DownloadPath = "",
                  CompletedPath = request.CompletedPath ?? "",
                  SelectedFileIds = request.SelectedFileIds
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add torrent {InfoHash} in background task", meta.InfoHash);
            }
        });

        return Results.Accepted($"/torrents/{meta.InfoHash}", new { infoHash = meta.InfoHash });
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

            if (!bitfieldManager.TryGetVerificationBitfield(torrent.InfoHash, out var bitfield))
                continue;

            var status = await statusCache.GetStatus(torrent.InfoHash);
            var summary = new TorrentSummaryVm
            {
                Name = meta.Name,
                InfoHash = meta.InfoHash,
                Status = status.ToString(),
                Progress = bitfieldManager.GetWantedCompletionRatio(torrent.InfoHash, bitfield),
                TotalSizeBytes = meta.SelectedTotalSize,
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
            Progress = bitfieldManager.GetWantedCompletionRatio(infoHash, verificationBitfield),
            TotalSizeBytes = meta.SelectedTotalSize,
            DownloadRate = rates.GetIngressRate(infoHash),
            UploadRate = rates.GetEgressRate(infoHash),
            BytesDownloaded = rates.GetTotalDownloaded(infoHash),
            BytesUploaded = rates.GetTotalUploaded(infoHash),
            Peers = peerSummaries,
        };


        return new TorrentGetResponse(summary);
    });

app.MapGet(
    "/torrents/{infoHash}/detail",
    async (InfoHash infoHash, TorrentMetadataCache cache, PeerSwarm swarms, BitfieldManager bitfieldManager, TorrentStatusCache statusCache, TorrentStats rates, IFileSelectionService fileSelection) =>
    {
        if (!cache.TryGet(infoHash, out var meta))
            return TorrentDetailResponse.ErrorResponse(ErrorCode.Unknown);

        if (!bitfieldManager.TryGetVerificationBitfield(infoHash, out var verificationBitfield))
            return TorrentDetailResponse.ErrorResponse(ErrorCode.Unknown);

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
        var selectedFileIds = await fileSelection.GetSelectedFileIds(infoHash);

        var files = meta.Files.Select(f => new TorrentFileVm
        {
            Id = f.Id,
            Filename = f.Filename,
            Size = f.FileSize,
            IsSelected = selectedFileIds.Contains(f.Id)
        });

        var bitfieldBytes = verificationBitfield.Bytes;
        var bitfieldVm = new BitfieldVm
        {
            PieceCount = verificationBitfield.NumberOfPieces,
            HaveCount = (int)(verificationBitfield.CompletionRatio * verificationBitfield.NumberOfPieces),
            Bitfield = Convert.ToBase64String(bitfieldBytes)
        };

        var detail = new TorrentDetailVm
        {
            InfoHash = meta.InfoHash,
            Name = meta.Name,
            Status = status.ToString(),
            Progress = verificationBitfield.CompletionRatio,
            TotalSizeBytes = meta.PieceSize * meta.NumberOfPieces,
            DownloadRate = rates.GetIngressRate(infoHash),
            UploadRate = rates.GetEgressRate(infoHash),
            BytesDownloaded = rates.GetTotalDownloaded(infoHash),
            BytesUploaded = rates.GetTotalUploaded(infoHash),
            Peers = peerSummaries,
            Bitfield = bitfieldVm,
            Files = files,
        };

        return new TorrentDetailResponse(detail);
    });

app.MapPost(
    "/torrents/{infoHash}/files/select",
    async (InfoHash infoHash, FileSelectionRequest request, TorrentMetadataCache cache, IFileSelectionService fileSelection, FileSelectionPieceMap pieceMap, TorrentEventBus eventBus) =>
    {
        if (!cache.TryGet(infoHash, out _))
            return ActionResponse.ErrorResponse(ErrorCode.Unknown);

        await fileSelection.SetSelectedFileIds(infoHash, request.FileIds);

        // Recompute allowed-pieces bitfield so the download engine picks up changes immediately
        await pieceMap.Recompute(infoHash);

        // Notify UI clients that file selection has changed
        await eventBus.PublishFileSelectionChanged(new FileSelectionChangedEvent
        {
            InfoHash = infoHash,
            SelectedFileIds = request.FileIds
        });

        return ActionResponse.SuccessResponse;
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

app.MapGet("settings/integrations", async (SettingsManager mgr) =>
{
    var settings = await mgr.GetIntegrationsSettings();
    return new IntegrationsSettingsGetResponse(settings);
});

app.MapPost("settings/integrations", async (IntegrationsSettingsUpdateRequest request, SettingsManager mgr) =>
{
    await mgr.SaveIntegrationsSettings(new()
    {
        SlackEnabled = request.SlackEnabled,
        SlackWebhookUrl = request.SlackWebhookUrl,
        SlackMessageTemplate = request.SlackMessageTemplate,
        SlackTriggerDownloadComplete = request.SlackTriggerDownloadComplete,
        DiscordEnabled = request.DiscordEnabled,
        DiscordWebhookUrl = request.DiscordWebhookUrl,
        DiscordMessageTemplate = request.DiscordMessageTemplate,
        DiscordTriggerDownloadComplete = request.DiscordTriggerDownloadComplete,
        CommandHookEnabled = request.CommandHookEnabled,
        CommandTemplate = request.CommandTemplate,
        CommandWorkingDirectory = request.CommandWorkingDirectory,
        CommandTriggerDownloadComplete = request.CommandTriggerDownloadComplete
    });
    return ActionResponse.SuccessResponse;
});

app.MapGet("filesystem/directories", (string? path) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(path))
            return Results.Ok(new { Data = BuildRootDirectoryBrowseVm() });

        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            return Results.Ok(new { Data = BuildRootDirectoryBrowseVm() });

        var parentPath = Directory.GetParent(fullPath)?.FullName;
        var directories = GetDirectoryChildren(fullPath);
        return Results.Ok(new
        {
            Data = new DirectoryBrowseVm
            {
                CurrentPath = fullPath,
                ParentPath = parentPath,
                CanNavigateUp = !string.IsNullOrWhiteSpace(parentPath),
                Directories = directories
            }
        });
    }
    catch
    {
        return Results.Ok(new { Data = BuildRootDirectoryBrowseVm() });
    }
});


var initService = app.Services.GetRequiredService<InitializationService>();
await initService.Initialize(CancellationToken.None);

await app.RunAsync();


static void WireEventBus(IServiceProvider services)
{
    var bus = services.GetRequiredService<TorrentEventBus>();

    // --- Hot path: piece validation (channel-based, sequential) ---
    var validator = services.GetRequiredService<PieceValidator>();
    bus.OnPieceValidationRequest(validator.ValidateAsync);

    // --- Hot path: piece verified -> broadcast Have to peers + SignalR batch ---
    var swarmDispatcher = services.GetRequiredService<PeerSwarmMessageDispatcher>();
    bus.OnPieceVerified(swarmDispatcher.HandlePieceVerified);

    var hubDispatcher = services.GetRequiredService<TorrentHubMessageDispatcher>();
    var statusMaintainer = services.GetRequiredService<TorrentStatusCacheMaintainer>();
    var verificationTracker = services.GetRequiredService<TorrentVerificationTracker>();
    bus.OnPieceVerified(hubDispatcher.HandlePieceVerified);

    // --- Torrent complete -> post-download actions + SignalR ---
    var postDownload = services.GetRequiredService<PostDownloadActionExecutor>();
    bus.OnTorrentComplete(postDownload.HandleTorrentComplete);
    bus.OnTorrentComplete(hubDispatcher.HandleTorrentComplete);
    bus.OnTorrentVerificationStarted(statusMaintainer.HandleVerificationStarted);
    bus.OnTorrentVerificationStarted(hubDispatcher.HandleTorrentVerificationStarted);
    bus.OnTorrentVerificationCompleted(statusMaintainer.HandleVerificationCompleted);
    bus.OnTorrentVerificationCompleted(hubDispatcher.HandleTorrentVerificationCompleted);
    bus.OnFileCopyStarted(statusMaintainer.HandleFileCopyStarted);
    bus.OnFileCopyStarted(hubDispatcher.HandleTorrentFileCopyStarted);
    bus.OnFileCopyCompleted(statusMaintainer.HandleFileCopyCompleted);
    bus.OnFileCopyCompleted(hubDispatcher.HandleTorrentFileCopyCompleted);

    // --- Lifecycle events -> status cache + announce service + SignalR ---
    var announceHandler = services.GetRequiredService<AnnounceServiceEventHandler>();

    bus.OnTorrentAdded(hubDispatcher.HandleTorrentAdded);

    bus.OnTorrentStarted(statusMaintainer.HandleTorrentStarted);
    bus.OnTorrentStarted(announceHandler.HandleTorrentStarted);
    bus.OnTorrentStarted(hubDispatcher.HandleTorrentStarted);

    bus.OnTorrentStopped(statusMaintainer.HandleTorrentStopped);
    bus.OnTorrentStopped(announceHandler.HandleTorrentStopped);
    bus.OnTorrentStopped(hubDispatcher.HandleTorrentStopped);

    bus.OnTorrentRemoved(statusMaintainer.HandleTorrentRemoved);
    bus.OnTorrentRemoved(announceHandler.HandleTorrentRemoved);
    bus.OnTorrentRemoved(hubDispatcher.HandleTorrentRemoved);
    bus.OnTorrentRemoved(verificationTracker.HandleTorrentRemoved);

    // --- Peer events -> SignalR only ---
    bus.OnPeerConnected(hubDispatcher.HandlePeerConnected);
    bus.OnPeerDisconnected(hubDispatcher.HandlePeerDisconnected);
    bus.OnPeerBitfieldReceived(hubDispatcher.HandlePeerBitfieldReceived);

    // --- Stats -> SignalR ---
    bus.OnTorrentStats(hubDispatcher.HandleTorrentStats);

    // --- File selection changed -> SignalR ---
    bus.OnFileSelectionChanged(hubDispatcher.HandleFileSelectionChanged);

    // Start the validation channel background reader
    bus.Start();
}


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

static DirectoryBrowseVm BuildRootDirectoryBrowseVm()
{
    var roots = OperatingSystem.IsWindows()
        ? Directory.GetLogicalDrives()
        : [Path.GetPathRoot(Environment.CurrentDirectory) ?? Path.DirectorySeparatorChar.ToString()];

    return new DirectoryBrowseVm
    {
        CurrentPath = string.Empty,
        ParentPath = null,
        CanNavigateUp = false,
        Directories = roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray()
    };
}

static string[] GetDirectoryChildren(string fullPath)
{
    try
    {
        return Directory.GetDirectories(fullPath)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    catch (UnauthorizedAccessException)
    {
        return [];
    }
    catch (DirectoryNotFoundException)
    {
        return [];
    }
}

sealed class DirectoryBrowseVm
{
    public required string CurrentPath { get; init; }
    public required string[] Directories { get; init; }
    public string? ParentPath { get; init; }
    public bool CanNavigateUp { get; init; }
}

internal class InitializationService(
    IServiceProvider serviceProvider,
    TorrentTaskManager taskManager,
    IMetadataFileService metaFileService,
    FileSelectionService fileSelectionService,
    FileSelectionPieceMap fileSelectionPieceMap,
    TorrentStatusCache statusCache,
    IServiceScopeFactory scopeFactory,
    AnnounceServiceState announceServiceState,
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

            // Load persisted file selection or default to all files selected
            var selectedIds = await fileSelectionService.GetSelectedFileIds(torrentMeta.InfoHash);
            if (selectedIds.Count == 0)
                fileSelectionService.InitializeAllSelected(torrentMeta.InfoHash, torrentMeta.Files.Select(f => f.Id));

            // Compute allowed-pieces bitfield from file selection
            await fileSelectionPieceMap.Recompute(torrentMeta.InfoHash);

            var config = await GetFromDatabase(torrentMeta.InfoHash);
            if (config?.Status != null)
                statusCache.UpdateStatus(torrentMeta.InfoHash, config.Status);

            if (config?.Status == TorrentStatus.Running)
            {
                announceServiceState.AddTorrent(torrentMeta);
                await taskManager.Start(torrentMeta.InfoHash);
            }
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
