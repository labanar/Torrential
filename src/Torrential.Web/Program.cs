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
        var summaries = new List<object>();

        //Stitch in all the appropriate data
        await foreach (var torrents in torrentDb.Torrents.AsAsyncEnumerable())
        {
            if (!metaCache.TryGet(torrents.InfoHash, out var meta)) continue;



            var peers = await swarms.GetPeers(torrents.InfoHash);
            var peerSummaries = peers.Select(x => new
            {
                PeerId = x.PeerId.ToAsciiString(),
                IpAddress = x.PeerInfo.Ip.ToString(),
                Port = x.PeerInfo.Port,
                BytesDownloaded = x.BytesDownloaded,
                IsSeed = x.State?.PeerBitfield?.HasAll() ?? false,
                CompletionRatio = x.State?.PeerBitfield?.CompletionRatio ?? 0
            });

            if (!bitfieldManager.TryGetDownloadBitfield(torrents.InfoHash, out var downloadBitfield))
                continue;

            var summary = new
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
    (InfoHash infoHash, TorrentMetadataCache cache) =>
    {
        cache.TryGet(infoHash, out var meta);
        return meta;
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
    async (InfoHash infoHash, ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse> handler) =>
        await handler.Execute(new() { InfoHash = infoHash }));

app.MapPost("settings",
       async (FileSettingsUpdateCommand command, ICommandHandler<FileSettingsUpdateCommand, FileSettingsUpdateResponse> handler) =>
              await handler.Execute(command));

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
db.Database.Migrate();


//Load settings + create defaults
using var scope2 = app.Services.CreateScope();
var db2 = scope2.ServiceProvider.GetRequiredService<TorrentialDb>();
var settings = await db2.Settings.FindAsync(TorrentialSettings.DefaultId);
if (settings == null)
{
    var appPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    await db2.Settings.AddAsync(new()
    {
        FileSettings = new FileSettings
        {
            DownloadPath = Path.Combine(appPath, "torrential\\downloads"),
            CompletedPath = Path.Combine(appPath, "torrential\\completed")
        }
    });
    await db2.SaveChangesAsync();
}

//Load settings into memory cache

using var scope3 = app.Services.CreateScope();
var db3 = scope3.ServiceProvider.GetRequiredService<TorrentialDb>();
var cache = scope3.ServiceProvider.GetRequiredService<IMemoryCache>();
var settings2 = await db3.Settings.FindAsync(TorrentialSettings.DefaultId);
cache.Set("settings.file", settings2.FileSettings);


//Load torrents
using var scope4 = app.Services.CreateScope();
var db4 = scope4.ServiceProvider.GetRequiredService<TorrentialDb>();
var mgr = scope4.ServiceProvider.GetRequiredService<TorrentTaskManager>();
var metaFileService = scope4.ServiceProvider.GetRequiredService<IMetadataFileService>();
await foreach (var torrentMeta in metaFileService.GetAllMetadataFiles())
{
    await mgr.Add(torrentMeta);
}


//Go through the file system and get all metadata files

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
