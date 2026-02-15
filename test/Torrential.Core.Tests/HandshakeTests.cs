using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Torrential.Core.Tests;

public class HandshakeTests
{
    private static readonly byte[] ProtocolBytes = "BitTorrent protocol"u8.ToArray();

    private static InfoHash CreateTestInfoHash()
    {
        return InfoHash.FromHexString("0102030405060708091011121314151617181920");
    }

    [Fact]
    public void WriteHandshake_Produces68Bytes_WithCorrectProtocolString()
    {
        var pipe = new Pipe();
        var infoHash = CreateTestInfoHash();
        var selfId = PeerId.New;

        HandshakeService.WriteHandshake(pipe.Writer, infoHash, selfId);
        pipe.Writer.FlushAsync().GetAwaiter().GetResult();
        pipe.Writer.Complete();

        var result = pipe.Reader.ReadAsync().GetAwaiter().GetResult();
        var buffer = result.Buffer;

        // Handshake must be exactly 68 bytes
        Assert.Equal(68, buffer.Length);

        // First byte is the protocol string length (19)
        Span<byte> data = stackalloc byte[68];
        buffer.CopyTo(data);
        Assert.Equal(19, data[0]);

        // Bytes 1-19 are the protocol identifier
        Assert.True(data[1..20].SequenceEqual(ProtocolBytes));

        // Bytes 28-47 should match the info hash
        Span<byte> expectedHash = stackalloc byte[20];
        infoHash.CopyTo(expectedHash);
        Assert.True(data[28..48].SequenceEqual(expectedHash));

        // Bytes 48-67 should match the peer id
        Span<byte> expectedPeerId = stackalloc byte[20];
        selfId.CopyTo(expectedPeerId);
        Assert.True(data[48..68].SequenceEqual(expectedPeerId));

        pipe.Reader.Complete();
    }

    [Fact]
    public void ParseHandshake_ValidHandshake_ReturnsSuccess()
    {
        var infoHash = CreateTestInfoHash();
        var peerId = PeerId.New;

        // Build a valid 68-byte handshake
        var handshake = new byte[68];
        handshake[0] = 19;
        ProtocolBytes.CopyTo(handshake.AsSpan(1));
        // Reserved bytes 20-27 are zero (already default)
        Span<byte> hashBytes = stackalloc byte[20];
        infoHash.CopyTo(hashBytes);
        hashBytes.CopyTo(handshake.AsSpan(28));
        Span<byte> peerIdBytes = stackalloc byte[20];
        peerId.CopyTo(peerIdBytes);
        peerIdBytes.CopyTo(handshake.AsSpan(48));

        var sequence = new ReadOnlySequence<byte>(handshake);
        var response = HandshakeService.ParseHandshake(ref sequence);

        Assert.True(response.Success);
        Assert.Equal(HandshakeError.NONE, response.Error);
        Assert.Equal(infoHash, response.InfoHash);
        Assert.Equal(peerId, response.PeerId);
    }

    [Fact]
    public void ParseHandshake_InvalidProtocolIdentifier_ReturnsError()
    {
        var handshake = new byte[68];
        handshake[0] = 19;
        // Write garbage instead of "BitTorrent protocol"
        "INVALID protocol xx"u8.CopyTo(handshake.AsSpan(1));
        // Rest can be zeroes

        var sequence = new ReadOnlySequence<byte>(handshake);
        var response = HandshakeService.ParseHandshake(ref sequence);

        Assert.False(response.Success);
        Assert.Equal(HandshakeError.INVALID_PROTOCOL, response.Error);
    }

    [Fact]
    public async Task HandleOutbound_SendsHandshakeAndParsesResponse()
    {
        var service = new HandshakeService(NullLogger<HandshakeService>.Instance);
        var infoHash = CreateTestInfoHash();
        var selfId = PeerId.New;
        var remotePeerId = PeerId.New;

        // Outbound: we write to outPipe, peer reads from it
        // Peer writes to inPipe, we read from it
        var outPipe = new Pipe();
        var inPipe = new Pipe();

        // Simulate the remote peer's response by writing a valid handshake to inPipe
        var remoteHandshake = new byte[68];
        remoteHandshake[0] = 19;
        ProtocolBytes.CopyTo(remoteHandshake.AsSpan(1));
        infoHash.CopyTo(remoteHandshake.AsSpan(28));
        remotePeerId.CopyTo(remoteHandshake.AsSpan(48));

        await inPipe.Writer.WriteAsync(remoteHandshake);
        inPipe.Writer.Complete();

        var response = await service.HandleOutbound(outPipe.Writer, inPipe.Reader, infoHash, selfId, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(infoHash, response.InfoHash);
        Assert.Equal(remotePeerId, response.PeerId);

        outPipe.Writer.Complete();
        outPipe.Reader.Complete();
        inPipe.Reader.Complete();
    }

    [Fact]
    public async Task HandleInbound_ValidHashCallback_SendsResponseAndReturnsSuccess()
    {
        var service = new HandshakeService(NullLogger<HandshakeService>.Instance);
        var infoHash = CreateTestInfoHash();
        var selfId = PeerId.New;
        var remotePeerId = PeerId.New;

        var inPipe = new Pipe();
        var outPipe = new Pipe();

        // Simulate remote peer sending a handshake
        var remoteHandshake = new byte[68];
        remoteHandshake[0] = 19;
        ProtocolBytes.CopyTo(remoteHandshake.AsSpan(1));
        infoHash.CopyTo(remoteHandshake.AsSpan(28));
        remotePeerId.CopyTo(remoteHandshake.AsSpan(48));

        await inPipe.Writer.WriteAsync(remoteHandshake);
        inPipe.Writer.Complete();

        var response = await service.HandleInbound(outPipe.Writer, inPipe.Reader, selfId, hash => hash == infoHash, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(infoHash, response.InfoHash);
        Assert.Equal(remotePeerId, response.PeerId);

        outPipe.Writer.Complete();
        outPipe.Reader.Complete();
        inPipe.Reader.Complete();
    }

    [Fact]
    public async Task HandleInbound_InvalidHashCallback_ReturnsInvalidHashError()
    {
        var service = new HandshakeService(NullLogger<HandshakeService>.Instance);
        var infoHash = CreateTestInfoHash();
        var selfId = PeerId.New;
        var remotePeerId = PeerId.New;

        var inPipe = new Pipe();
        var outPipe = new Pipe();

        var remoteHandshake = new byte[68];
        remoteHandshake[0] = 19;
        ProtocolBytes.CopyTo(remoteHandshake.AsSpan(1));
        infoHash.CopyTo(remoteHandshake.AsSpan(28));
        remotePeerId.CopyTo(remoteHandshake.AsSpan(48));

        await inPipe.Writer.WriteAsync(remoteHandshake);
        inPipe.Writer.Complete();

        // Callback always returns false — hash not recognized
        var response = await service.HandleInbound(outPipe.Writer, inPipe.Reader, selfId, _ => false, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(HandshakeError.INVALID_HASH, response.Error);

        outPipe.Writer.Complete();
        outPipe.Reader.Complete();
        inPipe.Reader.Complete();
    }
}
