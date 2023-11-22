using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Trackers;


var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddSingleton<PieceSelector>();
builder.Services.AddSingleton<PeerManager>();
builder.Services.AddSingleton<BitfieldManager>();
builder.Services.AddSingleton<TorrentMetadataCache>();
builder.Services.AddHostedService<Runner>();

var app = builder.Build();
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

internal class Runner(PeerManager peerMgr, IPeerService peerService, IEnumerable<ITrackerClient> trackerClients, BitfieldManager bitfieldMgr, PieceSelector pieceSelector, IFileSegmentSaveService segmentSaveService, TorrentMetadataCache cache, ILogger<Runner> logger)
    : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var meta = TorrentMetadataParser.FromFile("./debian-12.0.0-amd64-netinst.iso.torrent");
        cache.Add(meta);

        foreach (var tracker in trackerClients)
        {
            if (!tracker.IsValidAnnounceForClient(meta.AnnounceList.First())) continue;
            var announceResponse = await tracker.Announce(new AnnounceRequest
            {
                InfoHash = meta.InfoHash,
                PeerId = peerService.Self.Id,
                Url = meta.AnnounceList.First(),
                NumWant = 50
            });

            await peerMgr.ConnectToPeers(meta, announceResponse, 50);
            bitfieldMgr.Initialize(meta.InfoHash, meta.NumberOfPieces);

            var tasks = new List<Task>();
            foreach (var peer in peerMgr.ConnectedPeers[meta.InfoHash])
            {
                tasks.Add(InitiatePeer(meta, peer));
            }
            await Task.WhenAll(tasks);
        }
    }

    public async Task InitiatePeer(TorrentMetadata meta, PeerWireClient peer)
    {
        //Send in the runtime deps here
        //Piece selection strategy
        //Piece queue
        //Rate limiting service

        //After a piece goes through the verification queue, we need to update our bitfield
        //We also need to broadcast to each peer that we now have this new piece
        //This is after an ENTIRE piece is downloaded and verified (not a segment of a piece, but the full piece)
        var processor = peer.Process(meta, bitfieldMgr, segmentSaveService, CancellationToken.None);

        //await peer.SendBitfield(new Bitfield2(meta.NumberOfPieces));
        while (peer.State.Bitfield == null)
            await Task.Delay(100);

        await peer.SendIntereted();
        while (peer.State.AmChoked)
            await Task.Delay(100);


        while (!peer.State.AmChoked)
        {
            //Start asking for pieces, wait for us to get a piece back then ask for the next piece
            var idx = pieceSelector.SuggestNextPiece(meta.InfoHash, peer.State.Bitfield);
            if (idx == null)
            {
                await Task.Delay(250);
                continue;
            }

            var requestSize = (int)Math.Pow(2, 14);
            var remainder = (int)meta.PieceSize;
            while (remainder > 0)
            {
                var offset = (int)meta.PieceSize - remainder;
                await peer.SendPieceRequest(idx.Value, offset, requestSize);
                remainder -= requestSize;
            }

            //TODO - this responsibility should be handled after we verify that the piece hash is good
            //For now I'll artificially set the piece to high in our bitfield
            if (bitfieldMgr.TryGetBitfield(meta.InfoHash, out var myBitfield))
                myBitfield.MarkHave(idx.Value);
        }

        await processor;
    }
}

internal class PeerManager
{
    private readonly IPeerService _peerService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PeerManager> _logger;
    public Dictionary<InfoHash, List<PeerWireClient>> ConnectedPeers = [];

    public PeerManager(IPeerService peerService, ILoggerFactory loggerFactory, ILogger<PeerManager> logger)
    {
        _peerService = peerService;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public async Task ConnectToPeers(TorrentMetadata meta, AnnounceResponse announceResponse, int maxPeers)
    {
        var peerConnectionDict = new Dictionary<PeerWireConnection, Task<PeerConnectionResult>>(announceResponse.Peers.Count);
        for (int i = 0; i < announceResponse.Peers.Count; i++)
        {
            var conn = new PeerWireConnection(_peerService, new System.Net.Sockets.TcpClient(), _loggerFactory.CreateLogger<PeerWireConnection>());
            peerConnectionDict.Add(conn, conn.Connect(meta.InfoHash, announceResponse.Peers.ElementAt(i), TimeSpan.FromSeconds(5)));
        }
        await Task.WhenAll(peerConnectionDict.Values);

        var numConnected = 0;
        foreach (var (conn, connectTask) in peerConnectionDict)
        {
            var result = await connectTask;
            if (!result.Success || numConnected >= maxPeers)
            {
                conn.Dispose();
                continue;
            }

            //Based on the extensions in the handshake, we should know enough to wire up any runtime dependencies
            //1) Piece selection strategy

            numConnected++;
            _logger.LogInformation("Connected to peer");
            if (ConnectedPeers.TryGetValue(meta.InfoHash, out var connectedPeers))
                connectedPeers.Add(new PeerWireClient(conn, _logger));
            else
                ConnectedPeers[meta.InfoHash] = [new PeerWireClient(conn, _logger)];
        }

        peerConnectionDict.Clear();
    }
}




internal class PieceSelector(BitfieldManager bitfieldManager)
{
    public int? SuggestNextPiece(InfoHash infohash, Bitfield peerBitfield)
    {
        if (!bitfieldManager.TryGetBitfield(infohash, out var myBitfield))
            return null;


        return myBitfield.SuggestPieceToDownload(peerBitfield);
    }
}
