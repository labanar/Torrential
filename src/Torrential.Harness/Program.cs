using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Torrential.Core;
using Torrential.Harness;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger("Harness");

if (args.Length == 0)
{
    Console.WriteLine("Usage: Torrential.Harness <path-to-torrent-file>");
    return 1;
}

var torrentPath = args[0];
if (!File.Exists(torrentPath))
{
    Console.WriteLine($"File not found: {torrentPath}");
    return 1;
}

// 1. Parse torrent file
TorrentInfo torrent;
try
{
    torrent = TorrentFileParser.FromFile(torrentPath);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to parse torrent file: {ex.Message}");
    return 1;
}

var sizeMB = torrent.TotalSize / (1024.0 * 1024.0);
var sizeDisplay = sizeMB >= 1024
    ? $"{sizeMB / 1024.0:F1} GB"
    : $"{sizeMB:F0} MB";

logger.LogInformation("Parsed torrent: {Name} ({Size}, {Pieces} pieces, piece size {PieceSize})",
    torrent.Name, sizeDisplay, torrent.NumberOfPieces, torrent.PieceSize);

if (torrent.AnnounceUrls.Count == 0)
{
    Console.WriteLine("No HTTP trackers found in torrent file.");
    return 1;
}

// 2. Announce to tracker to get peers
var selfId = PeerId.New;
logger.LogInformation("Our peer ID: {PeerId}", selfId.ToAsciiString());

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
var trackerClient = new SimpleTrackerClient(httpClient, loggerFactory.CreateLogger("Tracker"));

List<TrackerPeer> peers = [];
foreach (var announceUrl in torrent.AnnounceUrls)
{
    peers = await trackerClient.Announce(announceUrl, torrent.InfoHash, selfId, torrent.TotalSize);
    if (peers.Count > 0)
    {
        logger.LogInformation("Got {Count} peers from tracker", peers.Count);
        break;
    }
}

if (peers.Count == 0)
{
    Console.WriteLine("No peers found from any tracker.");
    return 1;
}

// 3. Try to connect to peers
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var handshakeLogger = loggerFactory.CreateLogger<HandshakeService>();
var handshakeService = new HandshakeService(handshakeLogger);
var connLogger = loggerFactory.CreateLogger("Connection");

PeerWireSocketConnection? activeConnection = null;
HandshakeResponse handshakeResult = default;

foreach (var peer in peers)
{
    if (cts.IsCancellationRequested) break;

    logger.LogInformation("Connecting to {Ip}:{Port}...", peer.Ip, peer.Port);

    Socket? socket = null;
    try
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        connectCts.CancelAfter(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(peer.Ip, peer.Port, connectCts.Token);

        var connection = new PeerWireSocketConnection(socket, connLogger);
        connection.SetInfoHash(torrent.InfoHash);

        // Perform outbound handshake
        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        handshakeCts.CancelAfter(TimeSpan.FromSeconds(10));

        handshakeResult = await handshakeService.HandleOutbound(
            connection.Writer,
            connection.Reader,
            torrent.InfoHash,
            selfId,
            handshakeCts.Token);

        if (!handshakeResult.Success)
        {
            logger.LogWarning("Handshake failed with {Ip}:{Port} - {Error}", peer.Ip, peer.Port, handshakeResult.Error);
            await connection.DisposeAsync();
            continue;
        }

        connection.SetPeerId(handshakeResult.PeerId);
        activeConnection = connection;
        logger.LogInformation("Handshake successful with peer {PeerId}", handshakeResult.PeerId.ToAsciiString());
        break;
    }
    catch (Exception ex)
    {
        logger.LogWarning("Failed to connect to {Ip}:{Port}: {Error}", peer.Ip, peer.Port, ex.Message);
        socket?.Dispose();
    }
}

if (activeConnection == null)
{
    Console.WriteLine("Could not connect to any peer.");
    return 1;
}

// 4. Start protocol message processing
await using var connection2 = activeConnection;
var clientLogger = loggerFactory.CreateLogger("PeerWire");
var client = new PeerWireClient(activeConnection, torrent.NumberOfPieces, clientLogger, cts.Token);
var processTask = client.ProcessMessages();

// 5. Send our empty bitfield (we have nothing)
using (var emptyBitfield = new Bitfield(torrent.NumberOfPieces))
{
    await client.SendBitfield(emptyBitfield);
    logger.LogInformation("Sent empty bitfield");
}

// 6. Wait for peer's bitfield and unchoke
var waitStart = DateTime.UtcNow;
while (client.State.PeerBitfield == null || client.State.PeerBitfield.HasNone())
{
    if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(10))
    {
        logger.LogWarning("Timed out waiting for peer bitfield");
        break;
    }
    await Task.Delay(100, cts.Token);
}

if (client.State.PeerBitfield != null)
{
    logger.LogInformation("Received peer bitfield: {Completion:F1}% complete",
        client.State.PeerBitfield.CompletionRatio * 100);
}

// 7. Send Interested
await client.SendInterested();
logger.LogInformation("Sent Interested");

// 8. Wait for Unchoke
waitStart = DateTime.UtcNow;
while (client.State.AmChoked)
{
    if (DateTime.UtcNow - waitStart > TimeSpan.FromSeconds(15))
    {
        logger.LogWarning("Timed out waiting for Unchoke");
        await client.DisposeAsync();
        return 1;
    }
    await Task.Delay(100, cts.Token);
}
logger.LogInformation("Received Unchoke");

// 9. Request piece 0 block by block
const int blockSize = 16384;
var pieceSize = torrent.PieceSize;
// Last piece might be smaller
var requestPieceSize = torrent.NumberOfPieces == 1
    ? torrent.TotalSize
    : pieceSize;
var numBlocks = (int)((requestPieceSize + blockSize - 1) / blockSize);
var lastBlockSize = (int)(requestPieceSize - (long)(numBlocks - 1) * blockSize);

logger.LogInformation("Requesting piece 0 ({PieceSize} bytes, {NumBlocks} blocks)", requestPieceSize, numBlocks);

for (int i = 0; i < numBlocks; i++)
{
    var offset = i * blockSize;
    var length = (i == numBlocks - 1) ? lastBlockSize : blockSize;
    await client.SendPieceRequest(pieceIndex: 0, begin: offset, length: length);
}

// 10. Read blocks and log them
var receivedBlocks = 0;
var pieceTimeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
pieceTimeout.CancelAfter(TimeSpan.FromSeconds(30));

try
{
    await foreach (var block in client.InboundBlocks.ReadAllAsync(pieceTimeout.Token))
    {
        using (block)
        {
            logger.LogInformation("Received block: piece={PieceIndex} offset={Offset} size={Size}",
                block.PieceIndex, block.Offset, block.Buffer.Length);
            receivedBlocks++;

            if (receivedBlocks >= numBlocks)
                break;
        }
    }
}
catch (OperationCanceledException)
{
    logger.LogWarning("Timed out waiting for blocks ({Received}/{Expected})", receivedBlocks, numBlocks);
}

if (receivedBlocks >= numBlocks)
{
    logger.LogInformation("Piece 0 fully received (discarded). Disconnecting.");
}
else
{
    logger.LogInformation("Received {Received}/{Expected} blocks. Disconnecting.", receivedBlocks, numBlocks);
}

await client.DisposeAsync();
return 0;
