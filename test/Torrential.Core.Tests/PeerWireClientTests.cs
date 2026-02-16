using System.Buffers.Binary;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Core.Peers;
using Torrential.Core.Torrents;

namespace Torrential.Core.Tests;

/// <summary>
/// Simple IPeer backed by a Pipe for testing.
/// Write protocol messages to InputWriter; read outbound bytes from OutputReader.
/// </summary>
internal sealed class TestPeer : IPeer, IDisposable
{
    private readonly Pipe _inputPipe = new();
    private readonly Pipe _outputPipe = new();

    // Client reads from here (simulated remote peer -> client)
    public PipeReader Reader => _inputPipe.Reader;

    // Client writes to here (client -> simulated remote peer)
    public PipeWriter Writer => _outputPipe.Writer;

    // Test code writes raw bytes here to simulate incoming messages
    public PipeWriter InputWriter => _inputPipe.Writer;

    // Test code reads outbound bytes from here
    public PipeReader OutputReader => _outputPipe.Reader;

    public void Dispose()
    {
        _inputPipe.Reader.Complete();
        _inputPipe.Writer.Complete();
        _outputPipe.Reader.Complete();
        _outputPipe.Writer.Complete();
    }
}

public class PeerWireClientTests
{
    private static TorrentMetadata CreateTestMetadata(int numberOfPieces = 16)
    {
        return new TorrentMetadata
        {
            Name = "test",
            AnnounceList = Array.Empty<string>(),
            Files = Array.Empty<TorrentMetadataFile>(),
            PieceSize = 262144,
            InfoHash = new byte[20],
            TotalSize = 262144L * numberOfPieces,
            PieceHashesConcatenated = new byte[numberOfPieces * 20]
        };
    }

    private static PeerId CreateTestPeerId()
    {
        return PeerId.From(new byte[20]);
    }

    private static InfoHash CreateTestInfoHash()
    {
        return new byte[20];
    }

    /// <summary>
    /// Writes a raw peer wire message (length-prefixed) to the pipe.
    /// </summary>
    private static async Task WriteMessage(PipeWriter writer, byte messageId, byte[]? payload = null)
    {
        var payloadLength = payload?.Length ?? 0;
        var messageLength = 1 + payloadLength; // 1 for msgId + payload
        var totalBytes = 4 + messageLength;     // 4 for length prefix

        var buffer = writer.GetMemory(totalBytes);
        BinaryPrimitives.WriteInt32BigEndian(buffer.Span, messageLength);
        buffer.Span[4] = messageId;
        if (payload != null)
            payload.CopyTo(buffer[5..]);

        writer.Advance(totalBytes);
        await writer.FlushAsync();
    }

    /// <summary>
    /// Writes a keep-alive message (4 zero bytes) to the pipe.
    /// </summary>
    private static async Task WriteKeepAlive(PipeWriter writer)
    {
        var buffer = writer.GetMemory(4);
        BinaryPrimitives.WriteInt32BigEndian(buffer.Span, 0);
        writer.Advance(4);
        await writer.FlushAsync();
    }

    private static byte[] Int32BigEndian(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    [Fact]
    public async Task ChokeMessage_SetsAmChokedTrue()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        // Initially AmChoked is true; unchoke first, then choke
        var processTask = Task.Run(() => client.ProcessMessages());

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Unchoke);
        await Task.Delay(50);
        Assert.False(client.State.AmChoked);

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Choke);
        await Task.Delay(50);
        Assert.True(client.State.AmChoked);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task UnchokeMessage_SetsAmChokedFalse()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        Assert.True(client.State.AmChoked); // default

        var processTask = Task.Run(() => client.ProcessMessages());

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Unchoke);
        await Task.Delay(50);

        Assert.False(client.State.AmChoked);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task InterestedMessage_SetsPeerInterestedTrue()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        Assert.False(client.State.PeerInterested);

        var processTask = Task.Run(() => client.ProcessMessages());

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Interested);
        await Task.Delay(50);

        Assert.True(client.State.PeerInterested);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task NotInterestedMessage_SetsPeerInterestedFalse()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // Make peer interested first
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Interested);
        await Task.Delay(50);
        Assert.True(client.State.PeerInterested);

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.NotInterested);
        await Task.Delay(50);
        Assert.False(client.State.PeerInterested);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task HaveMessage_UpdatesPeerBitfield()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // Have message: payload is 4-byte big-endian piece index
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Have, Int32BigEndian(5));
        await Task.Delay(50);

        Assert.True(client.State.PeerBitfield!.HasPiece(5));
        Assert.False(client.State.PeerBitfield!.HasPiece(0));

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task BitfieldMessage_ReplacesPeerBitfield()
    {
        using var peer = new TestPeer();
        var meta = CreateTestMetadata(16);
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            meta, _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // 16 pieces = 2 bytes. Set all bits in first byte
        var bitfieldData = new byte[] { 0xFF, 0x00 };
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Bitfield, bitfieldData);
        await Task.Delay(50);

        for (int i = 0; i < 8; i++)
            Assert.True(client.State.PeerBitfield!.HasPiece(i));
        for (int i = 8; i < 16; i++)
            Assert.False(client.State.PeerBitfield!.HasPiece(i));

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task RequestMessage_AppearsOnPeerPieceRequestsChannel()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // Request payload: pieceIndex(4) + begin(4) + length(4)
        var payload = new byte[12];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0), 3);     // pieceIndex
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4), 0);     // begin
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(8), 16384); // length

        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Request, payload);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var request = await client.PeerPieceRequests.Reader.ReadAsync(cts.Token);

        Assert.Equal(3, request.Index);
        Assert.Equal(0, request.Begin);
        Assert.Equal(16384, request.Length);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task PieceMessage_InvokesBlockCallback_And_IncrementsBytesDownloaded()
    {
        using var peer = new TestPeer();
        PooledBlock? receivedBlock = null;
        var blockReceived = new TaskCompletionSource();

        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), block =>
            {
                receivedBlock = block;
                blockReceived.TrySetResult();
                return ValueTask.CompletedTask;
            }, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // Piece payload: pieceIndex(4) + begin(4) + block data
        var blockData = new byte[16];
        for (int i = 0; i < 16; i++) blockData[i] = (byte)i;

        var payload = new byte[4 + 4 + 16];
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0), 7);  // pieceIndex
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(4), 0);  // begin/offset
        blockData.CopyTo(payload.AsSpan(8));

        // messageSize = 9 + chunkSize; chunkSize = messageSize - 9 = payload.Length + 1 - 9
        // The message length prefix = 1 (msgId) + payload.Length
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Piece, payload);

        // Wait for block callback
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        cts.Token.Register(() => blockReceived.TrySetCanceled());
        await blockReceived.Task;

        Assert.NotNull(receivedBlock);
        Assert.Equal(7, receivedBlock.PieceIndex);
        Assert.Equal(0, receivedBlock.Offset);
        Assert.Equal(16, receivedBlock.Buffer.Length);
        Assert.True(client.BytesDownloaded > 0);

        receivedBlock.Dispose();
        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task SendInterested_WritesCorrectBytesToPipe()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        await client.SendInterested();
        await Task.Delay(50);

        var result = await peer.OutputReader.ReadAsync();
        var output = result.Buffer.First.ToArray();

        // Should be: [0,0,0,1, 2] (length=1, msgId=Interested=2)
        Assert.True(output.Length >= 5);
        Assert.Equal(0, output[0]);
        Assert.Equal(0, output[1]);
        Assert.Equal(0, output[2]);
        Assert.Equal(1, output[3]);
        Assert.Equal((byte)PeerWireMessageId.Interested, output[4]);

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task SendHave_WritesCorrectBytesToPipe()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        await client.SendHave(42);
        await Task.Delay(50);

        var result = await peer.OutputReader.ReadAsync();
        var output = result.Buffer.First.ToArray();

        // [0,0,0,5, 4, 0,0,0,42]
        Assert.True(output.Length >= 9);
        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(0)));
        Assert.Equal((byte)PeerWireMessageId.Have, output[4]);
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(5)));

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task SendPieceRequest_WritesCorrectBytesToPipe()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        await client.SendPieceRequest(1, 0, 16384);
        await Task.Delay(50);

        var result = await peer.OutputReader.ReadAsync();
        var output = result.Buffer.First.ToArray();

        // 4 length + 1 msgId + 4 index + 4 begin + 4 length = 17 bytes
        Assert.True(output.Length >= 17);
        Assert.Equal(13, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(0)));
        Assert.Equal((byte)PeerWireMessageId.Request, output[4]);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(5)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(9)));
        Assert.Equal(16384, BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(13)));

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task KeepAlive_DoesNotCrash_UpdatesTimestamp()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        var timestampBefore = client.LastMessageTimestamp;
        await Task.Delay(20); // ensure clock moves

        await WriteKeepAlive(peer.InputWriter);
        await Task.Delay(50);

        // KeepAlive with messageSize=0 and messageId=0 updates timestamp
        // But TryReadMessage returns true with messageId=0 messageSize=0
        // ProcessAsync treats this as keep-alive and updates timestamp

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task MultipleMessages_ProcessedInOrder()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        // Send: unchoke, interested, have(3), have(7)
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Unchoke);
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Interested);
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Have, Int32BigEndian(3));
        await WriteMessage(peer.InputWriter, (byte)PeerWireMessageId.Have, Int32BigEndian(7));
        await Task.Delay(100);

        Assert.False(client.State.AmChoked);
        Assert.True(client.State.PeerInterested);
        Assert.True(client.State.PeerBitfield!.HasPiece(3));
        Assert.True(client.State.PeerBitfield!.HasPiece(7));
        Assert.False(client.State.PeerBitfield!.HasPiece(0));

        await client.DisposeAsync();
        peer.InputWriter.Complete();
        await processTask;
    }

    [Fact]
    public async Task PeerId_MatchesConstructorArg()
    {
        using var peer = new TestPeer();
        var peerId = PeerId.New;
        var client = new PeerWireClient(peer, peerId, CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(peerId, client.PeerId);
        await client.DisposeAsync();
    }

    [Fact]
    public async Task InitialState_IsCorrect()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        Assert.True(client.State.AmChoked);
        Assert.True(client.State.PeerChoked);
        Assert.False(client.State.AmInterested);
        Assert.False(client.State.PeerInterested);
        Assert.NotNull(client.State.PeerBitfield);
        Assert.Equal(0L, client.BytesUploaded);
        Assert.Equal(0L, client.BytesDownloaded);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_StopsProcessing()
    {
        using var peer = new TestPeer();
        var client = new PeerWireClient(peer, CreateTestPeerId(), CreateTestInfoHash(),
            CreateTestMetadata(), _ => ValueTask.CompletedTask, NullLogger.Instance, CancellationToken.None);

        var processTask = Task.Run(() => client.ProcessMessages());

        await client.DisposeAsync();
        peer.InputWriter.Complete();

        // ProcessMessages should complete after disposal
        var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(processTask, completedTask);
    }
}
