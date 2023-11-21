using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.Grafana.Loki;
using Torrential;
using Torrential.Peers;
using Torrential.Trackers;


var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(BuildLogger(builder.Configuration));
builder.Services.AddTorrential();
builder.Services.AddSingleton<PeerManager>();
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

internal class Runner : BackgroundService
{
    private readonly PeerManager _peerMgr;
    private IEnumerable<ITrackerClient> _trackerClients;
    private readonly IPeerService _peerService;
    private readonly ILogger<Runner> _logger;

    public Runner(PeerManager peerMgr, IPeerService peerService, IEnumerable<ITrackerClient> trackerClients, ILogger<Runner> logger)
    {
        _peerMgr = peerMgr;
        _trackerClients = trackerClients;
        _peerService = peerService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var meta = TorrentMetadataParser.FromFile("./debian-12.0.0-amd64-netinst.iso.torrent");
        foreach (var tracker in _trackerClients)
        {
            if (!tracker.IsValidAnnounceForClient(meta.AnnounceList.First())) continue;
            var announceResponse = await tracker.Announce(new AnnounceRequest
            {
                InfoHash = meta.InfoHash,
                PeerId = _peerService.Self.Id,
                Url = meta.AnnounceList.First(),
                NumWant = 50
            });

            await _peerMgr.ConnectToPeers(meta, announceResponse, 50);
            var tasks = new List<Task>();
            foreach (var peer in _peerMgr.ConnectedPeers[meta.InfoHash])
            {
                tasks.Add(InitiatePeer(meta, peer));
            }
            await Task.WhenAll(tasks);
        }
    }

    public async Task InitiatePeer(TorrentMetadata meta, PeerWireClient peer)
    {
        var processor = peer.Process(CancellationToken.None);

        //await peer.SendBitfield(new Bitfield2(meta.NumberOfPieces));
        while (peer.State.Bitfield == null)
        {
            //_logger.LogInformation("Waiting for peer Bitfield");
            await Task.Delay(100);
        }


        await peer.SendIntereted();
        while (peer.State.AmChoked)
        {
            await Task.Delay(100);
        }

        //Start asking for pieces, wait for us to get a piece back then ask for the next piece
        var requestSize = (int)Math.Pow(2, 14);
        for (int pieceIndex = 0; pieceIndex < meta.NumberOfPieces; pieceIndex++)
        {
            var remainder = (int)meta.PieceSize;

            while (remainder > 0)
            {
                var offset = (int)meta.PieceSize - remainder;

                //there is an internal queue that will block once full, so we can just hammer away with piece requests
                await peer.SendPieceRequest(pieceIndex, offset, requestSize);
                remainder -= requestSize;
            }
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
            //2) 


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

