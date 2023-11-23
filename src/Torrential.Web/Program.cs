using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Peers;
using Torrential.Torrents;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddSingleton<PieceSelector>();
builder.Services.AddSingleton<PeerManager>();
builder.Services.AddSingleton<BitfieldManager>();
builder.Services.AddSingleton<TorrentMetadataCache>();
builder.Services.AddSingleton<TorrentRunner>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<TorrentManager>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("gcMode", () => System.Runtime.GCSettings.IsServerGC);

app.MapPost("/torrents/add", async (IFormFile file, TorrentManager manager) =>
{
    var meta = TorrentMetadataParser.FromStream(file.OpenReadStream());
    return manager.Add(meta);
})
.DisableAntiforgery();

app.MapGet("/torrents", () =>
{
    return Results.Ok();
});

app.MapGet("/torrents/{infoHash}", (InfoHash infoHash, TorrentMetadataCache cache) =>
{
    cache.TryGet(infoHash, out var meta);
    return infoHash;
});

app.MapPost("torrents/{infoHash}/start", (InfoHash infoHash, TorrentManager torrentManager) =>
{
    return torrentManager.Start(infoHash);
});

app.MapPost("torrents/{infoHash}/stop", async (InfoHash infoHash, TorrentManager torrentManager) =>
{
    return await torrentManager.Stop(infoHash);
});

app.MapPatch("torrents/{infoHash}/delete", (InfoHash infoHash, TorrentManager torrentManager) =>
{
    return torrentManager.Remove(infoHash);
});


app.Run();

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
