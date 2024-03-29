using MassTransit;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Extensions.SignalR;
using Torrential.Files;
using Torrential.Torrents;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
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
app.MapHub<TorrentHub>("/torrents/hub");

//app
//    .MapGet("/torrents/events", async (HttpContext context) =>
//    {
//        context.Response.Headers.Append("Content-Type", "text/event-stream");
//        context.Response.Headers.Append("Cache-Control", "no-cache");
//        context.Response.Headers.Append("Connection", "keep-alive");
//        await foreach (var item in TorrentEventDispatcher.EventReader.ReadAllAsync(context.RequestAborted))
//        {
//            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("data: "));
//            await JsonSerializer.SerializeAsync(context.Response.Body, item);
//            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("\n\n"));
//            await context.Response.Body.FlushAsync();
//        }
//    });

app.MapPost(
    "/torrents/add",
    async (IFormFile file, TorrentManager manager) =>
    {
        var meta = TorrentMetadataParser.FromStream(file.OpenReadStream());
        return await manager.Add(meta);
    })
    .DisableAntiforgery();

app.MapGet(
    "/torrents",
    () =>
    {
        return Results.Ok();
    });

app.MapGet(
    "/torrents/{infoHash}",
    (InfoHash infoHash, TorrentMetadataCache cache) =>
    {
        cache.TryGet(infoHash, out var meta);
        return meta;
    });

app.MapPost(
    "torrents/{infoHash}/start",
    async (InfoHash infoHash, TorrentManager torrentManager) =>
       await torrentManager.Start(infoHash));

app.MapPost("torrents/{infoHash}/stop",
    async (InfoHash infoHash, TorrentManager torrentManager) =>
        await torrentManager.Stop(infoHash));

app.MapPatch("torrents/{infoHash}/delete",
    async (InfoHash infoHash, TorrentManager torrentManager) =>
       await torrentManager.Remove(infoHash));

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
