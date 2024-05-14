using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net.Sockets;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireConnection : IPeerWireConnection
{
    private PipeReader _peerReader = PipeReader.Create(Stream.Null);
    private PipeWriter _peerWriter = PipeWriter.Create(Stream.Null);
    private readonly TcpClient _client;
    private readonly ILogger<PeerWireConnection> _logger;

    public Guid Id { get; }
    public PeerId? PeerId { get; private set; }
    public PipeReader Reader => _peerReader;
    public PipeWriter Writer => _peerWriter;
    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }
    public DateTimeOffset ConnectionTimestamp { get; private set; } = DateTime.MinValue;
    public PeerInfo PeerInfo { get; private set; }


    public void SetInfoHash(InfoHash infoHash)
    {
        if (InfoHash != InfoHash.None)
            throw new InvalidOperationException("InfoHash already set");

        InfoHash = infoHash;
    }

    public void SetPeerId(PeerId peerId)
    {
        PeerId = peerId;
    }

    public PeerWireConnection(TcpClient client, ILogger<PeerWireConnection> logger)
    {
        Id = Guid.NewGuid();

        _peerReader = PipeReader.Create(client.GetStream());
        _peerWriter = PipeWriter.Create(client.GetStream());

        _client = client;
        _logger = logger;
    }

    public ValueTask DisposeAsync()
    {
        if (_client != null)
            _client.Dispose();

        return ValueTask.CompletedTask;
    }
}
