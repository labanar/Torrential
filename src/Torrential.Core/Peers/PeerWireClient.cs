using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;
using Torrential.Core.Torrents;

namespace Torrential.Core.Peers;

public sealed class PeerWireClient : IAsyncDisposable
{
    private const int DEFAULT_BLOCK_SIZE = 16384;
    private readonly IPeer _peer;
    private readonly PeerWireState _state;
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    private readonly TorrentMetadata _meta;
    private readonly InfoHash _infoHash;
    private readonly Func<PooledBlock, ValueTask> _onBlockReceived;

    public PeerId PeerId { get; }
    public PeerWireState State => _state;
    public long BytesUploaded { get; private set; }
    public long BytesDownloaded { get; private set; }
    public DateTimeOffset LastMessageTimestamp { get; private set; }

    private readonly Channel<PreparedPacket> _outboundChannel = Channel.CreateBounded<PreparedPacket>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<PooledBlock> _inboundBlockChannel = Channel.CreateBounded<PooledBlock>(new BoundedChannelOptions(10)
    {
        SingleReader = true,
        SingleWriter = true
    });

    public readonly Channel<PieceRequestMessage> PeerPieceRequests = Channel.CreateBounded<PieceRequestMessage>(new BoundedChannelOptions(5)
    {
        SingleReader = false,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });

    public PeerWireClient(
        IPeer peer,
        PeerId peerId,
        InfoHash infoHash,
        TorrentMetadata metadata,
        Func<PooledBlock, ValueTask> onBlockReceived,
        ILogger logger,
        CancellationToken torrentStoppingToken)
    {
        _peer = peer;
        PeerId = peerId;
        _infoHash = infoHash;
        _meta = metadata;
        _onBlockReceived = onBlockReceived;
        _logger = logger;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(torrentStoppingToken);
        _state = new PeerWireState();
        _state.PeerBitfield = new Bitfield(_meta.NumberOfPieces);
        LastMessageTimestamp = DateTimeOffset.UtcNow;
    }

    public async ValueTask QueuePacket<T>(T packet)
        where T : IPeerPacket<T>
    {
        await _outboundChannel.Writer.WriteAsync(PreparedPacket.FromPeerPacket(packet), _cts.Token);
    }

    private PreparedPacket CreatePacket<T>(T packet)
        where T : IPeerPacket<T>, allows ref struct
    {
        return PreparedPacket.FromPeerPacket(packet);
    }

    private async ValueTask QueuePreparedPacket(PreparedPacket packet)
    {
        await _outboundChannel.Writer.WriteAsync(packet, _cts.Token);
    }

    public async Task ProcessMessages()
    {
        var readerTask = ProcessReads(_cts.Token);
        var writeTask = ProcessWrites(_cts.Token);
        var savingTask = ProcessInboundBlocks(_cts.Token);

        try
        {
            await Task.WhenAll(readerTask, writeTask, savingTask);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing peer connection {PeerId}", PeerId);
        }

        _outboundChannel.Writer.Complete();
        _inboundBlockChannel.Writer.Complete();
        PeerPieceRequests.Writer.Complete();

        //Clear out any disposables stuck in the channels
        await foreach (var pak in _outboundChannel.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var pak in _inboundBlockChannel.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var _ in PeerPieceRequests.Reader.ReadAllAsync()) ;
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var pak in _outboundChannel.Reader.ReadAllAsync(cancellationToken))
            {
                using (pak)
                {
                    try
                    {
                        _peer.Writer.Write(pak.PacketData);
                        await _peer.Writer.FlushAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                        _logger.LogError("Failed to write message to {PeerId} - ending peer connection write processor", PeerId);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing writes");
        }

        _logger.LogInformation("Ending peer connection write processor");
    }

    private async Task ProcessInboundBlocks(CancellationToken cancellationToken)
    {
        await foreach (var block in _inboundBlockChannel.Reader.ReadAllAsync(cancellationToken))
            await _onBlockReceived(block);

        _logger.LogInformation("Ending peer connection block processor");
    }

    private async Task ProcessReads(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReadResult result = await _peer.Reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out var messageSize, out var messageId, out ReadOnlySequence<byte> payload))
                {
                    if (!await ProcessAsync(messageSize, messageId, payload))
                    {
                        _logger.LogInformation("Failed to process message {MessageId} {MessageSize} - ending peer connection read processor", messageId, messageSize);
                        return;
                    }
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                _peer.Reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    _logger.LogWarning("No further data from peer");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from peer");
                return;
            }
        }

        _logger.LogInformation("Ending peer connection read processor");
    }

    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out int messageSize, out byte messageId, out ReadOnlySequence<byte> payload)
    {
        payload = default;
        messageId = 0;
        messageSize = 0;

        if (buffer.Length < 4)
            return false;

        var sequenceReader = new SequenceReader<byte>(buffer);

        if (!sequenceReader.TryReadBigEndian(out messageSize))
        {
            _logger.LogDebug("Failed to read message size");
            return false;
        }

        if (messageSize == 0)
        {
            _logger.LogDebug("Keep alive received");
            buffer = buffer.Slice(4);
            return true;
        }

        if (!sequenceReader.TryRead(out messageId))
        {
            _logger.LogDebug("Failed to read messageId");
            return false;
        }

        if (messageSize == 1)
        {
            buffer = buffer.Slice(5);
            return true;
        }
        else if (!sequenceReader.TryReadExact(messageSize - 1, out payload))
        {
            _logger.LogDebug("Failed to read message payload");
            return false;
        }

        buffer = buffer.Slice(payload.End);
        return true;
    }

    private async ValueTask<bool> ProcessAsync(int messageSize, byte messageId, ReadOnlySequence<byte> payload)
    {
        //Discard keep alive messages
        if (messageId == 0 && messageSize == 0)
        {
            LastMessageTimestamp = DateTimeOffset.UtcNow;
            return true;
        }

        bool handled;
        switch ((PeerWireMessageId)messageId)
        {
            case PeerWireMessageId.Choke:
                HandleChoke();
                handled = true;
                break;
            case PeerWireMessageId.Unchoke:
                HandleUnchoke();
                handled = true;
                break;
            case PeerWireMessageId.Interested:
                HandleInterested();
                handled = true;
                break;
            case PeerWireMessageId.NotInterested:
                HandleNotInterested();
                handled = true;
                break;
            case PeerWireMessageId.Bitfield:
                HandleBitfield(payload);
                handled = true;
                break;
            case PeerWireMessageId.Have:
                HandleHave(payload);
                handled = true;
                break;
            case PeerWireMessageId.Piece:
                handled = await HandlePieceAsync(payload, messageSize - 9);
                break;
            case PeerWireMessageId.Request:
                handled = await HandleRequestAsync(payload);
                break;
            case PeerWireMessageId.Cancel:
                handled = HandleCancel(payload);
                break;
            case PeerWireMessageId.Port:
                handled = HandlePort(payload);
                break;
            default:
                _logger.LogWarning("Unhandled message type {MessageType}", messageId);
                handled = true;
                break;
        }

        LastMessageTimestamp = DateTimeOffset.UtcNow;
        return handled;
    }

    // --- Message Handlers ---

    private void HandleChoke()
    {
        _state.AmChoked = true;
        _state.LastChokedAt = DateTime.UtcNow;
    }

    private void HandleUnchoke()
    {
        _state.AmChoked = false;
    }

    private void HandleInterested()
    {
        _state.PeerInterested = true;
        _state.PeerLastInterestedAt = DateTime.UtcNow;
    }

    private void HandleNotInterested()
    {
        _state.PeerInterested = false;
    }

    private bool HandleHave(ReadOnlySequence<byte> payload)
    {
        var sequenceReader = new SequenceReader<byte>(payload);
        if (!sequenceReader.TryReadBigEndian(out int index))
            return false;

        _state.PeerBitfield?.MarkHave(index);
        return true;
    }

    private bool HandleBitfield(ReadOnlySequence<byte> payload)
    {
        var expectedSize = (_meta.NumberOfPieces + 7) / 8;
        if ((int)payload.Length != expectedSize)
            return false;

        byte[]? rented = null;
        Span<byte> buffer = expectedSize <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(expectedSize));
        try
        {
            buffer = buffer[..expectedSize];
            payload.CopyTo(buffer);
            _state.PeerBitfield!.Fill(buffer);
            return true;
        }
        finally
        {
            if (rented != null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private async ValueTask<bool> HandlePieceAsync(ReadOnlySequence<byte> payload, int chunkSize)
    {
        var block = PooledBlock.FromReadOnlySequence(payload, chunkSize, _infoHash);
        await _inboundBlockChannel.Writer.WriteAsync(block, _cts.Token);
        BytesDownloaded += block.Buffer.Length;
        return true;
    }

    private async ValueTask<bool> HandleRequestAsync(ReadOnlySequence<byte> payload)
    {
        var pieceRequest = PieceRequestMessage.FromReadOnlySequence(payload);
        await PeerPieceRequests.Writer.WriteAsync(pieceRequest);
        return true;
    }

    private bool HandleCancel(ReadOnlySequence<byte> payload)
    {
        var sequenceReader = new SequenceReader<byte>(payload);
        if (!sequenceReader.TryReadBigEndian(out int index))
            return false;
        if (!sequenceReader.TryReadBigEndian(out int begin))
            return false;
        if (!sequenceReader.TryReadBigEndian(out int length))
            return false;

        return true;
    }

    private bool HandlePort(ReadOnlySequence<byte> payload)
    {
        var reader = new SequenceReader<byte>(payload);
        if (!reader.TryReadBigEndian(out short port))
        {
            _logger.LogError("Error reading port value");
            return false;
        }
        return true;
    }

    // --- Send Methods ---

    public ValueTask SendChoke() => QueuePreparedPacket(CreatePacket(new ChokeMessage()));
    public ValueTask SendUnchoke() => QueuePreparedPacket(CreatePacket(new UnchokeMessage()));
    public ValueTask SendInterested() => QueuePreparedPacket(CreatePacket(new InterestedMessage()));
    public ValueTask SendNotInterested() => QueuePreparedPacket(CreatePacket(new NotInterestedMessage()));
    public ValueTask SendHave(int pieceIndex) => QueuePreparedPacket(CreatePacket(new HaveMessage(pieceIndex)));
    public ValueTask SendPieceRequest(int pieceIndex, int begin, int length = DEFAULT_BLOCK_SIZE) =>
        QueuePacket(new PieceRequestMessage(pieceIndex, begin, length));
    public ValueTask SendCancel(int pieceIndex, int begin, int length) => QueuePreparedPacket(CreatePacket(new CancelMessage(pieceIndex, begin, length)));
    public ValueTask SendBitfield(IBitfield bitfield) => QueuePreparedPacket(CreatePacket(new BitfieldMessage(bitfield)));

    public async ValueTask SendPiece(PreparedPacket pak)
    {
        await _outboundChannel.Writer.WriteAsync(pak, _cts.Token);
        BytesUploaded += pak.Size - 13;
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing peer client {PeerId}", PeerId);
        await _cts.CancelAsync();
        _cts.Dispose();
        _state.PeerBitfield?.Dispose();
    }
}
