using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Torrential.Core.Tests;

internal sealed class StubPeerWireConnection : IPeerWireConnection
{
    private readonly Pipe _ingressPipe = new();
    private readonly Pipe _egressPipe = new();

    public Guid Id { get; } = Guid.NewGuid();
    public PeerInfo PeerInfo { get; } = new(System.Net.IPAddress.Loopback, 6881);
    public PeerId? PeerId { get; private set; }
    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public DateTimeOffset ConnectionTimestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>The PipeReader that the client reads incoming data from.</summary>
    public PipeReader Reader => _ingressPipe.Reader;

    /// <summary>The PipeWriter that the client writes outgoing data to.</summary>
    public PipeWriter Writer => _egressPipe.Writer;

    /// <summary>Write bytes into the ingress pipe to simulate data arriving from the peer.</summary>
    public PipeWriter IngressWriter => _ingressPipe.Writer;

    /// <summary>Read bytes from the egress pipe to see what the client sent.</summary>
    public PipeReader EgressReader => _egressPipe.Reader;

    public void SetInfoHash(InfoHash infoHash) => InfoHash = infoHash;
    public void SetPeerId(PeerId peerId) => PeerId = peerId;

    public ValueTask DisposeAsync()
    {
        _ingressPipe.Reader.Complete();
        _ingressPipe.Writer.Complete();
        _egressPipe.Reader.Complete();
        _egressPipe.Writer.Complete();
        return ValueTask.CompletedTask;
    }
}

public class PeerWireClientTests
{
    private static InfoHash CreateTestInfoHash() =>
        InfoHash.FromHexString("0102030405060708091011121314151617181920");

    [Fact]
    public void Constructor_NullPeerId_Throws()
    {
        var conn = new StubPeerWireConnection();
        conn.SetInfoHash(CreateTestInfoHash());
        // PeerId is not set, so it should throw

        Assert.Throws<ArgumentException>(() =>
            new PeerWireClient(conn, 100, NullLogger.Instance, CancellationToken.None));
    }

    [Fact]
    public void TryReadMessage_KeepAlive_ReturnsTrueWithZeroSizeAndId()
    {
        // Keep-alive: 4 zero bytes (message length = 0)
        var data = new byte[] { 0, 0, 0, 0 };
        var buffer = new ReadOnlySequence<byte>(data);

        var result = PeerWireClient.TryReadMessage(ref buffer, out var messageSize, out var messageId, out var payload);

        Assert.True(result);
        Assert.Equal(0, messageSize);
        Assert.Equal(0, messageId);
        Assert.Equal(0, payload.Length);
        // Buffer should be fully consumed
        Assert.Equal(0, buffer.Length);
    }

    [Fact]
    public void TryReadMessage_Choke_ReturnsTrueWithCorrectId()
    {
        // Choke: length=1, id=0
        var data = new byte[] { 0, 0, 0, 1, 0 };
        var buffer = new ReadOnlySequence<byte>(data);

        var result = PeerWireClient.TryReadMessage(ref buffer, out var messageSize, out var messageId, out var payload);

        Assert.True(result);
        Assert.Equal(1, messageSize);
        Assert.Equal(PeerWireMessageType.Choke, messageId);
        Assert.Equal(0, payload.Length);
        Assert.Equal(0, buffer.Length);
    }

    [Fact]
    public void TryReadMessage_Have_ReturnsTrueWithPieceIndex()
    {
        // Have: length=5, id=4, piece index (big-endian)
        var pieceIndex = 42;
        var data = new byte[9];
        // Length = 5
        data[0] = 0; data[1] = 0; data[2] = 0; data[3] = 5;
        // Message ID = Have (4)
        data[4] = PeerWireMessageType.Have;
        // Piece index = 42 (big-endian)
        data[5] = 0; data[6] = 0; data[7] = 0; data[8] = 42;

        var buffer = new ReadOnlySequence<byte>(data);

        var result = PeerWireClient.TryReadMessage(ref buffer, out var messageSize, out var messageId, out var payload);

        Assert.True(result);
        Assert.Equal(5, messageSize);
        Assert.Equal(PeerWireMessageType.Have, messageId);
        Assert.Equal(4, payload.Length);

        // Verify the piece index in the payload
        var reader = new SequenceReader<byte>(payload);
        Assert.True(reader.TryReadBigEndian(out int parsedIndex));
        Assert.Equal(pieceIndex, parsedIndex);

        Assert.Equal(0, buffer.Length);
    }

    [Fact]
    public void TryReadMessage_IncompleteBuffer_ReturnsFalse()
    {
        // Only 3 bytes — not enough for the 4-byte length prefix
        var data = new byte[] { 0, 0, 0 };
        var buffer = new ReadOnlySequence<byte>(data);

        var result = PeerWireClient.TryReadMessage(ref buffer, out _, out _, out _);

        Assert.False(result);
        // Buffer should be unchanged
        Assert.Equal(3, buffer.Length);
    }

    [Fact]
    public void TryReadMessage_IncompletePaylod_ReturnsFalse()
    {
        // Have message requires 9 bytes total, but we only provide 7
        var data = new byte[] { 0, 0, 0, 5, PeerWireMessageType.Have, 0, 0 };
        var buffer = new ReadOnlySequence<byte>(data);

        var result = PeerWireClient.TryReadMessage(ref buffer, out _, out _, out _);

        Assert.False(result);
    }
}
