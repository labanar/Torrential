using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Torrential.Trackers;
using Torrential.Utilities;

namespace Torrential.Peers;

public class PeerWireConnection : IPeerWireConnection
{
    //Empty until support for extensions is added
    static byte[] EMPTY_RESERVED = [0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
    static ReadOnlySpan<byte> PROTOCOL_BYTES => "BitTorrent protocol"u8;

    private PipeReader _peerReader = PipeReader.Create(Stream.Null);
    private PipeWriter _peerWriter = PipeWriter.Create(Stream.Null);
    private readonly TcpClient _client;
    private readonly IPeerService _peerService;
    private readonly ILogger<PeerWireConnection> _logger;

    public Guid Id { get; }

    private PeerInfo _peerInfo;

    public PeerId? PeerId { get; private set; }
    public PipeReader Reader => _peerReader;
    public PipeWriter Writer => _peerWriter;
    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }
    public PeerInfo PeerInfo => _peerInfo;

    public PeerWireConnection(IPeerService peerService, TcpClient client, ILogger<PeerWireConnection> logger)
    {
        Id = Guid.NewGuid();
        _client = client;
        _peerService = peerService;
        _logger = logger;
    }

    public async Task<PeerConnectionResult> Connect(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken)
    {
        if (!await TryEstablishConnection(peer, cancellationToken))
        {
            _client.Dispose();
            return PeerConnectionResult.Failure;
        }
        _peerReader = PipeReader.Create(_client.GetStream());
        _peerWriter = PipeWriter.Create(_client.GetStream());
        var handshakeResult = await Handshake(infoHash, cancellationToken);
        if (!handshakeResult.Success)
        {
            _client.Dispose();
            return PeerConnectionResult.Failure;
        }

        _peerInfo = peer;
        PeerId = handshakeResult.PeerId;
        InfoHash = infoHash;
        IsConnected = true;
        return PeerConnectionResult.FromHandshake(handshakeResult);
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

    private async Task<HandshakeResponse> Handshake(InfoHash infoHash, CancellationToken cancellationToken)
    {
        await SendHandshake(_peerWriter, infoHash);
        return await WaitForHandshake(infoHash, cancellationToken);
    }

    private async Task<HandshakeResponse> WaitForHandshake(InfoHash infoHash, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadOnlySequence<byte> buffer;
            ReadResult result;
            try
            {
                result = await _peerReader.ReadAsync(cancellationToken);
                buffer = result.Buffer;
            }
            catch (OperationCanceledException)
            {
                return new HandshakeResponse(HandshakeError.CANCELLED);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Handshake error");
                return new HandshakeResponse(HandshakeError.FATAL);
            }

            if (TryReadHandshake(ref buffer, out var handshakeBytes))
            {
                var resp = ParseHandshake(handshakeBytes, infoHash);
                _peerReader.AdvanceTo(buffer.Start, buffer.End);
                return resp;
            }
            else
            {
                _peerReader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (result.IsCompleted)
            {
                break;
            }
        }

        _logger.LogWarning("Handshake timed out");
        return new HandshakeResponse(HandshakeError.NO_RESPONSE);
    }
    private async ValueTask SendHandshake(PipeWriter peerWriter, InfoHash infoHash)
    {
        Writehandshake(infoHash);
        await peerWriter.FlushAsync();
    }


    private void Writehandshake(InfoHash infoHash)
    {
        var buffer = _peerWriter.GetSpan(68);
        buffer[0] = 19;
        PROTOCOL_BYTES.CopyTo(buffer[1..]);
        EMPTY_RESERVED.CopyTo(buffer[20..]);
        infoHash.CopyTo(buffer[28..]);
        _peerService.Self.Id.CopyTo(buffer[48..]);
        _peerWriter.Advance(68);
    }

    private bool TryReadHandshake(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> handshakeBytes)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        if (!sequenceReader.TryReadExact(68, out handshakeBytes))
            return false;

        buffer = buffer.Slice(handshakeBytes.End);
        return true;
    }
    private HandshakeResponse ParseHandshake(ReadOnlySequence<byte> handshakeBytes, InfoHash expectedHash)
    {
        var reader = new SequenceReader<byte>(handshakeBytes);

        if (!reader.TryRead(out var firstByte))
            return new HandshakeResponse(HandshakeError.NO_DATA);

        if (firstByte != 19)
        {
            _logger.LogInformation("Handshake failed - First byte did not match");
            return new HandshakeResponse(HandshakeError.INVALID_FIRST_BYTE);
        }

        if (!reader.TryReadExact(19, out var protocolSequence))
        {
            _logger.LogInformation("Handshake failed - Failed to read protocol identifier");
            return new HandshakeResponse(HandshakeError.PROTOCOL_NOT_RECEIVED);
        }

        Span<byte> protocolBuff = stackalloc byte[19];
        protocolSequence.CopyTo(protocolBuff);
        if (!protocolBuff.SequenceEqual(PROTOCOL_BYTES))
        {
            _logger.LogInformation("Handshake failed - Protocol identifier did not match");
            return new HandshakeResponse(HandshakeError.INVALID_PROTOCOL);
        }

        if (!reader.TryReadPeerExtensions(out var peerExtensions))
        {
            _logger.LogInformation("Handshake failed - Failed to read extensions");
            return new HandshakeResponse(HandshakeError.RESERVED_NOT_RECEIVED);
        }

        if (!reader.TryReadInfoHash(out var peerInfoHash))
        {
            _logger.LogInformation("Handshake failed - Failed to read info hash bytes");
            return new HandshakeResponse(HandshakeError.HASH_NOT_RECEIVED);
        }

        if (peerInfoHash != expectedHash)
        {
            _logger.LogInformation("Handshake failed - Info hash did not match");
            return new HandshakeResponse(HandshakeError.INVALID_HASH);
        }

        if (!reader.TryReadPeerId(out var peerId))
        {
            _logger.LogInformation("Handshake failed - Failed to read peerId");
            return new HandshakeResponse(HandshakeError.PEER_ID_NOT_RECEIVED);
        }

        return new HandshakeResponse(peerId, peerExtensions);
    }

    public void Dispose()
    {
        if (_client != null)
            _client.Dispose();
    }
}


internal readonly struct HandshakeResponse
{
    public readonly bool Success;
    public readonly HandshakeError Error = HandshakeError.NONE;
    public readonly PeerId PeerId;
    public readonly PeerExtensions Extensions;
    public HandshakeResponse(PeerId peerId, PeerExtensions extensions)
    {
        Success = true;
        PeerId = peerId;
        Extensions = extensions;
    }

    public HandshakeResponse(HandshakeError error)
    {
        Success = false;
        Error = error;
    }
}

internal enum HandshakeError : byte
{
    NONE,
    NO_RESPONSE,
    NO_DATA,
    INVALID_FIRST_BYTE,
    PROTOCOL_NOT_RECEIVED,
    INVALID_PROTOCOL,
    RESERVED_NOT_RECEIVED,
    HASH_NOT_RECEIVED,
    INVALID_HASH,
    PEER_ID_NOT_RECEIVED,
    CANCELLED,
    FATAL = byte.MaxValue,
}

