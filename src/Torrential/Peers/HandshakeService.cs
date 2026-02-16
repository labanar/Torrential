using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using Torrential.Torrents;
using Torrential.Utilities;

namespace Torrential.Peers;

public sealed class HandshakeService(IPeerService peerService, TorrentMetadataCache metaCache, ILogger<HandshakeService> logger)
{
    static ReadOnlySpan<byte> PROTOCOL_BYTES => "BitTorrent protocol"u8;

    public async Task<HandshakeResponse> HandleInbound(PipeWriter writer, PipeReader reader, CancellationToken stoppingToken)
    {
        var response = await WaitForHandshake(reader, stoppingToken);
        if (!response.Success)
        {
            logger.LogDebug("Handshake failed - {Error}", response.Error);
            return response;
        }

        if (!metaCache.TryGet(response.InfoHash, out _))
        {
            logger.LogWarning("Handshake failed - Torrent not found");
            return new HandshakeResponse(HandshakeError.INVALID_HASH);
        }

        logger.LogInformation("Handshake received");
        await SendHandshake(writer, response.InfoHash);
        logger.LogInformation("Handshake sent");
        return response;
    }


    public async Task<HandshakeResponse> HandleOutbound(PipeWriter writer, PipeReader reader, InfoHash infoHash, CancellationToken stoppingToken)
    {
        await SendHandshake(writer, infoHash);
        var response = await WaitForHandshake(reader, stoppingToken);
        if (response.InfoHash != infoHash)
        {
            logger.LogWarning("Handshake failed - Info hash did not match");
            return new HandshakeResponse(HandshakeError.INVALID_HASH);
        }

        return response;
    }


    private async Task<HandshakeResponse> WaitForHandshake(PipeReader reader, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadOnlySequence<byte> buffer;
            ReadResult result;
            try
            {
                result = await reader.ReadAsync(cancellationToken);
                buffer = result.Buffer;
            }
            catch (OperationCanceledException)
            {
                return new HandshakeResponse(HandshakeError.CANCELLED);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Handshake error");
                return new HandshakeResponse(HandshakeError.FATAL);
            }

            if (TryReadHandshake(ref buffer, out var handshakeBytes))
            {
                var resp = ParseHandshake(ref handshakeBytes);
                reader.AdvanceTo(buffer.Start, buffer.End);
                return resp;
            }
            else
            {
                reader.AdvanceTo(buffer.Start, buffer.End);
            }

            if (result.IsCompleted)
            {
                break;
            }
        }

        logger.LogDebug("Handshake timed out");
        return new HandshakeResponse(HandshakeError.NO_RESPONSE);
    }



    private async ValueTask SendHandshake(PipeWriter writer, InfoHash infoHash)
    {
        Writehandshake(writer, infoHash);
        await writer.FlushAsync();
    }


    private void Writehandshake(PipeWriter writer, InfoHash infoHash)
    {
        Span<byte> hashBytes = stackalloc byte[20];
        infoHash.CopyTo(hashBytes);
        Span<byte> peerIdBytes = stackalloc byte[20];
        peerService.Self.Id.CopyTo(peerIdBytes);
        var handshake = HandshakeData.Create(hashBytes, peerIdBytes);
        Span<byte> handshakeSpan = handshake;
        var buffer = writer.GetSpan(68);
        handshakeSpan.CopyTo(buffer);
        writer.Advance(68);
    }

    private bool TryReadHandshake(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> handshakeBytes)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        if (!sequenceReader.TryReadExact(68, out handshakeBytes))
            return false;

        buffer = buffer.Slice(handshakeBytes.End);
        return true;
    }
    private HandshakeResponse ParseHandshake(ref ReadOnlySequence<byte> handshakeBytes)
    {
        var reader = new SequenceReader<byte>(handshakeBytes);

        if (!reader.TryRead(out var firstByte))
            return new HandshakeResponse(HandshakeError.NO_DATA);

        if (firstByte != 19)
        {
            logger.LogInformation("Handshake failed - First byte did not match");
            return new HandshakeResponse(HandshakeError.INVALID_FIRST_BYTE);
        }

        if (!reader.TryReadExact(19, out var protocolSequence))
        {
            logger.LogDebug("Handshake failed - Failed to read protocol identifier");
            return new HandshakeResponse(HandshakeError.PROTOCOL_NOT_RECEIVED);
        }

        Span<byte> protocolBuff = stackalloc byte[19];
        protocolSequence.CopyTo(protocolBuff);
        if (!protocolBuff.SequenceEqual(PROTOCOL_BYTES))
        {
            logger.LogDebug("Handshake failed - Protocol identifier did not match");
            return new HandshakeResponse(HandshakeError.INVALID_PROTOCOL);
        }

        if (!reader.TryReadPeerExtensions(out var peerExtensions))
        {
            logger.LogDebug("Handshake failed - Failed to read extensions");
            return new HandshakeResponse(HandshakeError.RESERVED_NOT_RECEIVED);
        }

        if (!reader.TryReadInfoHash(out var peerInfoHash))
        {
            logger.LogDebug("Handshake failed - Failed to read info hash bytes");
            return new HandshakeResponse(HandshakeError.HASH_NOT_RECEIVED);
        }


        if (!reader.TryReadPeerId(out var peerId))
        {
            logger.LogDebug("Handshake failed - Failed to read peerId");
            return new HandshakeResponse(HandshakeError.PEER_ID_NOT_RECEIVED);
        }

        return new HandshakeResponse(peerInfoHash, peerId, peerExtensions);
    }
}


public readonly struct HandshakeResponse
{
    public readonly bool Success;
    public readonly HandshakeError Error = HandshakeError.NONE;
    public readonly PeerId PeerId;
    public readonly PeerExtensions Extensions;
    public readonly InfoHash InfoHash;
    public HandshakeResponse(InfoHash infoHash, PeerId peerId, PeerExtensions extensions)
    {
        Success = true;
        PeerId = peerId;
        Extensions = extensions;
        InfoHash = infoHash;
    }

    public HandshakeResponse(HandshakeError error)
    {
        Success = false;
        Error = error;
    }
}

public enum HandshakeError : byte
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

