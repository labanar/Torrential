using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;

namespace Torrential.Core;

public sealed class HandshakeService(ILogger<HandshakeService> logger)
{
    static readonly byte[] EMPTY_RESERVED = [0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0];
    static ReadOnlySpan<byte> PROTOCOL_BYTES => "BitTorrent protocol"u8;

    public async Task<HandshakeResponse> HandleInbound(
        PipeWriter writer,
        PipeReader reader,
        PeerId selfId,
        Func<InfoHash, bool> isValidInfoHash,
        CancellationToken stoppingToken)
    {
        var response = await WaitForHandshake(reader, stoppingToken);
        if (!response.Success)
        {
            logger.LogDebug("Handshake failed - {Error}", response.Error);
            return response;
        }

        if (!isValidInfoHash(response.InfoHash))
        {
            logger.LogWarning("Handshake failed - Torrent not found");
            return new HandshakeResponse(HandshakeError.INVALID_HASH);
        }

        logger.LogInformation("Handshake received");
        await SendHandshake(writer, response.InfoHash, selfId);
        logger.LogInformation("Handshake sent");
        return response;
    }

    public async Task<HandshakeResponse> HandleOutbound(
        PipeWriter writer,
        PipeReader reader,
        InfoHash infoHash,
        PeerId selfId,
        CancellationToken stoppingToken)
    {
        await SendHandshake(writer, infoHash, selfId);
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

    private async ValueTask SendHandshake(PipeWriter writer, InfoHash infoHash, PeerId selfId)
    {
        WriteHandshake(writer, infoHash, selfId);
        await writer.FlushAsync();
    }

    internal static void WriteHandshake(PipeWriter writer, InfoHash infoHash, PeerId selfId)
    {
        var buffer = writer.GetSpan(68);
        buffer[0] = 19;
        PROTOCOL_BYTES.CopyTo(buffer[1..]);
        EMPTY_RESERVED.CopyTo(buffer[20..]);
        infoHash.CopyTo(buffer[28..]);
        selfId.CopyTo(buffer[48..]);
        writer.Advance(68);
    }

    private static bool TryReadHandshake(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> handshakeBytes)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        if (!sequenceReader.TryReadExact(68, out handshakeBytes))
            return false;

        buffer = buffer.Slice(handshakeBytes.End);
        return true;
    }

    internal static HandshakeResponse ParseHandshake(ref ReadOnlySequence<byte> handshakeBytes)
    {
        var reader = new SequenceReader<byte>(handshakeBytes);

        if (!reader.TryRead(out var firstByte))
            return new HandshakeResponse(HandshakeError.NO_DATA);

        if (firstByte != 19)
            return new HandshakeResponse(HandshakeError.INVALID_FIRST_BYTE);

        if (!reader.TryReadExact(19, out var protocolSequence))
            return new HandshakeResponse(HandshakeError.PROTOCOL_NOT_RECEIVED);

        Span<byte> protocolBuff = stackalloc byte[19];
        protocolSequence.CopyTo(protocolBuff);
        if (!protocolBuff.SequenceEqual(PROTOCOL_BYTES))
            return new HandshakeResponse(HandshakeError.INVALID_PROTOCOL);

        if (!reader.TryReadPeerExtensions(out var peerExtensions))
            return new HandshakeResponse(HandshakeError.RESERVED_NOT_RECEIVED);

        if (!reader.TryReadInfoHash(out var peerInfoHash))
            return new HandshakeResponse(HandshakeError.HASH_NOT_RECEIVED);

        if (!reader.TryReadPeerId(out var peerId))
            return new HandshakeResponse(HandshakeError.PEER_ID_NOT_RECEIVED);

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
