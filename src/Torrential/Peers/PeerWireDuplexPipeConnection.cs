using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireDuplexPipeConnection : IPeerWireConnection
{
    private readonly ILogger _logger;
    private readonly ConnectionContext _ctx;

    public Guid Id { get; }

    public PeerInfo PeerInfo { get; private set; }

    public PeerId? PeerId { get; private set; }

    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }

    public PipeReader Reader { get; private set; }
    public PipeWriter Writer { get; private set; }

    private readonly HandshakeService _handshakeService;

    public PeerWireDuplexPipeConnection(ConnectionContext ctx, HandshakeService handshakeService, ILogger logger)
    {
        Id = Guid.NewGuid();
        _ctx = ctx;
        Reader = _ctx.Transport.Input;
        Writer = _ctx.Transport.Output;
        _handshakeService = handshakeService;
        _logger = logger;
    }

    public async Task<PeerConnectionResult> ConnectInbound(CancellationToken stoppingToken)
    {
        var peerHandshake = await _handshakeService.HandleInbound(Writer, Reader, stoppingToken);
        if (!peerHandshake.Success)
        {
            await _ctx.DisposeAsync();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = new PeerInfo
        {
            Ip = ((IPEndPoint)_ctx.RemoteEndPoint).Address,
            Port = ((IPEndPoint)_ctx.RemoteEndPoint).Port
        };
        PeerId = peerHandshake.PeerId;
        InfoHash = peerHandshake.InfoHash;
        IsConnected = true;
        return PeerConnectionResult.FromHandshake(peerHandshake);
    }

    public async Task<PeerConnectionResult> ConnectOutbound(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken)
    {
        var handshakeResult = await _handshakeService.HandleOutbound(Writer, Reader, infoHash, cancellationToken);
        if (!handshakeResult.Success)
        {
            await _ctx.DisposeAsync();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = peer;
        PeerId = handshakeResult.PeerId;
        InfoHash = infoHash;
        IsConnected = true;
        return PeerConnectionResult.FromHandshake(handshakeResult);
    }

    public void Dispose()
    {
        return;
    }
}
