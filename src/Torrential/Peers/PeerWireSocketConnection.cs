using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireSocketConnection : IPeerWireConnection
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly HandshakeService _handshakeService;
    private readonly TorrentMetadataCache _metaCache;

    public Guid Id { get; }

    public PeerInfo PeerInfo { get; private set; }

    public PeerId? PeerId { get; private set; }

    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }

    public DateTimeOffset ConnectionTimestamp { get; private set; } = DateTimeOffset.UtcNow;

    public PipeReader Reader { get; }
    public PipeWriter Writer { get; }

    public PeerWireSocketConnection(Socket socket, HandshakeService handshakeService, ILogger logger)
    {
        Id = Guid.NewGuid();
        _socket = socket;
        _stream = new NetworkStream(_socket, true);
        Reader = PipeReader.Create(_stream);
        Writer = PipeWriter.Create(_stream);

        _handshakeService = handshakeService;
    }

    public async Task<PeerConnectionResult> ConnectInbound(CancellationToken cancellationToken)
    {
        var peerHandshake = await _handshakeService.HandleInbound(Writer, Reader, cancellationToken);
        if (!peerHandshake.Success)
        {
            _socket.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = new PeerInfo
        {
            Ip = ((IPEndPoint)_socket.RemoteEndPoint).Address,
            Port = ((IPEndPoint)_socket.RemoteEndPoint).Port
        };
        PeerId = peerHandshake.PeerId;
        InfoHash = peerHandshake.InfoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(peerHandshake);

    }

    public async Task<PeerConnectionResult> ConnectOutbound(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken)
    {
        var handshakeResult = await _handshakeService.HandleOutbound(Writer, Reader, infoHash, cancellationToken);
        if (!handshakeResult.Success)
        {
            _socket.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = peer;
        PeerId = handshakeResult.PeerId;
        InfoHash = infoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(handshakeResult);
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}
