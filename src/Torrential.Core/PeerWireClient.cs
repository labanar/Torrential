using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;

namespace Torrential.Core;

public sealed class PeerWireClient : IAsyncDisposable
{
    private const int DEFAULT_BLOCK_SIZE = 16384;
    private readonly PeerWireState _state;
    private readonly CancellationTokenSource _cts;
    private readonly IPeerWireConnection _connection;
    private readonly ILogger _logger;
    private readonly int _numberOfPieces;
    private readonly InfoHash _infoHash;

    public PeerInfo PeerInfo => _connection.PeerInfo;

    private readonly Channel<PreparedPacket> _outboundMessages = Channel.CreateBounded<PreparedPacket>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<PooledBlock> _inboundBlocks = Channel.CreateBounded<PooledBlock>(new BoundedChannelOptions(10)
    {
        SingleReader = false,
        SingleWriter = true
    });

    public readonly Channel<PieceRequestMessage> PeerPieceRequests = Channel.CreateBounded<PieceRequestMessage>(new BoundedChannelOptions(5)
    {
        SingleReader = false,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });

    public ChannelReader<PooledBlock> InboundBlocks => _inboundBlocks.Reader;

    public PeerWireState State => _state;

    public long BytesUploaded { get; private set; }
    public long BytesDownloaded { get; private set; }
    public PeerId PeerId { get; private set; }

    public DateTimeOffset LastMessageTimestamp { get; private set; }

    public PeerWireClient(IPeerWireConnection connection, int numberOfPieces, ILogger logger, CancellationToken stoppingToken)
    {
        if (!connection.PeerId.HasValue)
            throw new ArgumentException("Peer Id must be set", nameof(connection));

        LastMessageTimestamp = connection.ConnectionTimestamp;
        PeerId = connection.PeerId.Value;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _state = new PeerWireState();

        _connection = connection;
        _logger = logger;
        _infoHash = connection.InfoHash;
        _numberOfPieces = numberOfPieces;
        _state.PeerBitfield = new Bitfield(_numberOfPieces);
    }


    public async Task ProcessMessages()
    {
        var readerTask = ProcessReads(_cts.Token);
        var writeTask = ProcessWrites(_cts.Token);

        try
        {
            await Task.WhenAll(readerTask, writeTask);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing peer connection {PeerId}", PeerId);
        }


        _outboundMessages.Writer.Complete();
        _inboundBlocks.Writer.Complete();
        PeerPieceRequests.Writer.Complete();

        //Clear out any disposables stuck in the channels
        await foreach (var pak in _outboundMessages.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var pak in _inboundBlocks.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var _ in PeerPieceRequests.Reader.ReadAllAsync()) ;
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        try
        {

            await foreach (var pak in _outboundMessages.Reader.ReadAllAsync(cancellationToken))
            {
                using (pak)
                {
                    try
                    {
                        _connection.Writer.Write(pak.AsSpan());
                        await _connection.Writer.FlushAsync(cancellationToken);
                    }
                    catch (Exception ex)
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

    private async Task ProcessReads(CancellationToken cancellationToken)
    {
        //TODO - enforce global and peer rate limits here
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReadResult result = await _connection.Reader.ReadAsync(cancellationToken);
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
                _connection.Reader.AdvanceTo(buffer.Start, buffer.End);

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

    internal static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out int messageSize, out byte messageId, out ReadOnlySequence<byte> payload)
    {
        payload = default;
        messageId = 0;
        messageSize = 0;

        //Make sure that we have at least 4 bytes
        //4 for the message size (32-bit) unsigned int
        if (buffer.Length < 4)
            return false;

        var sequenceReader = new SequenceReader<byte>(buffer);
        if (!sequenceReader.TryReadBigEndian(out messageSize))
            return false;

        if (messageSize == 0)
        {
            buffer = buffer.Slice(4);
            return true;
        }

        if (!sequenceReader.TryRead(out messageId))
            return false;

        if (messageSize == 1)
        {
            buffer = buffer.Slice(5);
            return true;
        }
        else if (!sequenceReader.TryReadExact(messageSize - 1, out payload))
        {
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


        //Certain messages can be processed immediately, so we'll handle those upfront
        bool handled;
        switch (messageId)
        {
            case PeerWireMessageType.Choke:
                HandleChoke();
                handled = true;
                break;
            case PeerWireMessageType.Unchoke:
                HandleUnchoke();
                handled = true;
                break;
            case PeerWireMessageType.Interested:
                HandleInterested();
                handled = true;
                break;
            case PeerWireMessageType.NotInterested:
                HandleNotInterested();
                handled = true;
                break;
            case PeerWireMessageType.Bitfield:
                HandleBitfield(payload);
                handled = true;
                break;
            case PeerWireMessageType.Have:
                HandleHave(payload);
                handled = true;
                break;
            case PeerWireMessageType.Piece:
                handled = await HandlePieceAsync(payload, messageSize - 9);
                break;
            case PeerWireMessageType.Request:
                handled = await HandleRequestAsync(payload);
                break;
            case PeerWireMessageType.Cancel:
                handled = HandleCancel(payload);
                break;
            default:
                _logger.LogWarning("Unhandled message type {MessageType}", messageId);
                handled = true;
                break;
        }

        LastMessageTimestamp = DateTimeOffset.UtcNow;
        return handled;
    }

    //These methods modify peer wire state and can be called from the main loop
    private bool HandleChoke()
    {
        _state.AmChoked = true;
        _state.LastChokedAt = DateTime.UtcNow;
        return true;
    }
    private bool HandleUnchoke()
    {
        _state.AmChoked = false;
        return true;
    }
    private bool HandleInterested()
    {
        _state.PeerInterested = true;
        _state.PeerLastInterestedAt = DateTime.UtcNow;
        return true;
    }
    private bool HandleNotInterested()
    {
        _state.PeerInterested = false;
        return true;
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
        Span<byte> buffer = stackalloc byte[(int)payload.Length];
        payload.CopyTo(buffer);
        var bitfield = new Bitfield(_numberOfPieces);
        bitfield.Fill(buffer);
        _state.PeerBitfield = bitfield;
        return true;
    }


    //This should be placed into a request channel and processed sequentially in a separate thread
    private async ValueTask<bool> HandleRequestAsync(ReadOnlySequence<byte> payload)
    {
        var pieceRequest = PieceRequestMessage.FromReadOnlySequence(payload);
        await PeerPieceRequests.Writer.WriteAsync(pieceRequest);
        return true;
    }

    private async ValueTask<bool> HandlePieceAsync(ReadOnlySequence<byte> payload, int chunkSize)
    {
        var block = PooledBlock.FromReadOnlySequence(payload, chunkSize, _infoHash);
        await _inboundBlocks.Writer.WriteAsync(block, _cts.Token);
        BytesDownloaded += block.Buffer.Length;
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
            return false;
        return true;
    }


    public async Task SendBitfield(IBitfield bitfield) => await WriteBitfieldAsync(bitfield);
    public async Task SendInterested() => await SendMessageAsync(PeerWireMessageType.Interested);
    public async Task SendNotInterested() => await SendMessageAsync(PeerWireMessageType.NotInterested);
    public async Task SendChoke() => await SendMessageAsync(PeerWireMessageType.Choke);
    public async Task SendUnchoke() => await SendMessageAsync(PeerWireMessageType.Unchoke);
    public async Task SendHave(int pieceIndex) => await SendMessageAsync(PeerWireMessageType.Have, pieceIndex);
    public async Task SendPieceRequest(int pieceIndex, int begin, int length = DEFAULT_BLOCK_SIZE) =>
        await SendMessageAsync(PeerWireMessageType.Request, pieceIndex, begin, length);

    public async Task SendPiece(PreparedPacket pak)
    {
        await _outboundMessages.Writer.WriteAsync(pak, _cts.Token);
        BytesUploaded += pak.Size - 13;
    }

    public async Task SendCancel(int pieceIndex, int begin, int length) => await SendMessageAsync(PeerWireMessageType.Cancel, pieceIndex, begin, length);

    private async Task SendMessageAsync(byte messageId) => await WritePacketAsync(messageId);
    private async Task SendMessageAsync(byte messageId, int p1) => await WritePacketAsync(messageId, p1);
    private async Task SendMessageAsync(byte messageId, int p1, int p2) => await WritePacketAsync(messageId, p1, p2);
    private async Task SendMessageAsync(byte messageId, int p1, int p2, int p3) => await WritePacketAsync(messageId, p1, p2, p3);

    private async Task WriteBitfieldAsync(IBitfield bitfield) =>
        await _outboundMessages.Writer.WriteAsync(MessagePacker.Pack(bitfield), _cts.Token);
    private async Task WritePacketAsync(byte messageId) =>
        await _outboundMessages.Writer.WriteAsync(MessagePacker.Pack(messageId), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1) =>
        await _outboundMessages.Writer.WriteAsync(MessagePacker.Pack(messageId, p1), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1, int p2) =>
        await _outboundMessages.Writer.WriteAsync(MessagePacker.Pack(messageId, p1, p2), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1, int p2, int p3) =>
        await _outboundMessages.Writer.WriteAsync(MessagePacker.Pack(messageId, p1, p2, p3), _cts.Token);


    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing peer client {PeerId}", PeerId);
        _cts.Cancel();
        await _connection.DisposeAsync();
    }
}
