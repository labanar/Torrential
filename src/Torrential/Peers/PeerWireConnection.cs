using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireConnection : IPeerWireConnection
{
    private PipeReader _peerReader = PipeReader.Create(Stream.Null);
    private PipeWriter _peerWriter = PipeWriter.Create(Stream.Null);
    private readonly TcpClient _client;
    private readonly HandshakeService _handshakeService;
    private readonly ILogger<PeerWireConnection> _logger;

    public Guid Id { get; }
    public PeerId? PeerId { get; private set; }
    public PipeReader Reader => _peerReader;
    public PipeWriter Writer => _peerWriter;
    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }
    public DateTimeOffset ConnectionTimestamp { get; private set; } = DateTime.MinValue;
    public PeerInfo PeerInfo { get; private set; }

    public PeerWireConnection(HandshakeService handshakeService, TcpClient client, ILogger<PeerWireConnection> logger)
    {
        Id = Guid.NewGuid();
        _client = client;
        _handshakeService = handshakeService;
        _logger = logger;
    }

    public async Task<PeerConnectionResult> ConnectOutbound(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken)
    {
        if (!await TryEstablishConnection(peer, cancellationToken))
        {
            _client.Dispose();
            return PeerConnectionResult.Failure;
        }
        _peerReader = PipeReader.Create(_client.GetStream());
        _peerWriter = PipeWriter.Create(_client.GetStream());


        var handshakeResult = await _handshakeService.HandleOutbound(_peerWriter, _peerReader, infoHash, cancellationToken);
        if (!handshakeResult.Success)
        {
            _client.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = peer;
        PeerId = handshakeResult.PeerId;
        InfoHash = infoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(handshakeResult);
    }

    public async Task<PeerConnectionResult> ConnectInbound(CancellationToken token)
    {
        _peerReader = PipeReader.Create(_client.GetStream());
        _peerWriter = PipeWriter.Create(_client.GetStream());

        var peerHandshake = await _handshakeService.HandleInbound(_peerWriter, _peerReader, token);
        if (!peerHandshake.Success)
        {
            _client.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = new PeerInfo
        {
            Ip = ((IPEndPoint)_client.Client.RemoteEndPoint).Address,
            Port = ((IPEndPoint)_client.Client.RemoteEndPoint).Port
        };
        PeerId = peerHandshake.PeerId;
        InfoHash = peerHandshake.InfoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(peerHandshake);
    }

    private async Task<bool> TryEstablishConnection(PeerInfo peer, CancellationToken cancellationToken)
    {
        var endpoint = new IPEndPoint(peer.Ip, peer.Port);

        try
        {
            await _client.ConnectAsync(endpoint, cancellationToken);
            _logger.LogInformation("Connection establied {Ip}:{Port}", peer.Ip, peer.Port);
            return true;
        }
        catch (SocketException sE)
        {
            if (sE.ErrorCode == 10061)
            {
                _logger.LogWarning("Connection refused {Ip}:{Port}", peer.Ip, peer.Port);
                return false;
            }

            _logger.LogWarning(sE, "Connection failure {Ip}:{Port}", peer.Ip, peer.Port);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Connection timed out {Ip}:{Port}", peer.Ip, peer.Port);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Connection failure {Ip}:{Port}", peer.Ip, peer.Port);
            return false;
        }

        return false;
    }

    public ValueTask DisposeAsync()
    {
        if (_client != null)
            _client.Dispose();

        return ValueTask.CompletedTask;
    }
}
