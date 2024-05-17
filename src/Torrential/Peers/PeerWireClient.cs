using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;
using Torrential.Files;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers;

public sealed class PeerWireState
{
    public DateTimeOffset LastChokedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool AmChoked { get; set; } = true;
    public bool PeerChoked { get; set; } = true;
    public bool AmInterested { get; set; } = false;
    public bool PeerInterested { get; set; } = false;
    public int PiecesReceived { get; set; } = 0;
    public Bitfield? PeerBitfield { get; set; } = null;
    public DateTimeOffset PeerLastInterestedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class PeerWireClient : IAsyncDisposable
{
    private const int PIECE_SEGMENT_REQUEST_SIZE = 16384;
    private readonly PeerWireState _state;
    private readonly CancellationTokenSource _cts;
    private readonly IPeerWireConnection _connection;
    private readonly ILogger _logger;

    public PeerInfo PeerInfo => _connection.PeerInfo;


    private readonly Channel<PreparedPacket> OUTBOUND_MESSAGES = Channel.CreateBounded<PreparedPacket>(new BoundedChannelOptions(5)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<PooledPieceSegment> PIECE_SEGMENT_CHANNEL = Channel.CreateBounded<PooledPieceSegment>(new BoundedChannelOptions(5)
    {
        SingleReader = false,
        SingleWriter = true
    });

    public readonly Channel<PieceRequestMessage> PeerPeieceRequests = Channel.CreateBounded<PieceRequestMessage>(new BoundedChannelOptions(5)
    {
        SingleReader = false,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });

    private TorrentMetadata _meta;
    private InfoHash _infoHash;
    private IFileSegmentSaveService _fileSegmentSaveService;

    public PeerWireState State => _state;

    public long BytesUploaded { get; private set; }
    public long BytesDownloaded { get; private set; }
    public PeerId PeerId { get; private set; }

    public DateTimeOffset LastMessageTimestamp { get; private set; }

    public PeerWireClient(IPeerWireConnection connection, TorrentMetadataCache metaCache, IFileSegmentSaveService fileSegmentSaveService, ILogger logger, CancellationToken torrentStoppingToken)
    {
        if (!connection.PeerId.HasValue)
            throw new ArgumentException("Peer Id must be set", nameof(connection));

        if (!metaCache.TryGet(connection.InfoHash, out _meta))
            throw new ArgumentException("Peer Id must be set", nameof(connection));

        LastMessageTimestamp = connection.ConnectionTimestamp;
        PeerId = connection.PeerId.Value;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(torrentStoppingToken);
        _state = new PeerWireState();

        _connection = connection;
        _fileSegmentSaveService = fileSegmentSaveService;
        _logger = logger;
        _infoHash = connection.InfoHash;
    }


    public async Task ProcessMessages()
    {
        var readerTask = ProcessReads(_cts.Token);
        var writeTask = ProcessWrites(_cts.Token);
        var savingTask = ProcessSegments(_cts.Token);

        try
        {
            await Task.WhenAll(readerTask, writeTask, savingTask);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing peer connection {PeerId}", PeerId);
        }


        OUTBOUND_MESSAGES.Writer.Complete();
        PIECE_SEGMENT_CHANNEL.Writer.Complete();
        PeerPeieceRequests.Writer.Complete();

        //Clear out any disposables stuck in the channels
        await foreach (var pak in OUTBOUND_MESSAGES.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var pak in PIECE_SEGMENT_CHANNEL.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var _ in PeerPeieceRequests.Reader.ReadAllAsync()) ;
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        try
        {

            await foreach (var pak in OUTBOUND_MESSAGES.Reader.ReadAllAsync(cancellationToken))
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

    private async Task ProcessSegments(CancellationToken cancellationToken)
    {
        await foreach (var segment in PIECE_SEGMENT_CHANNEL.Reader.ReadAllAsync(cancellationToken))
        {
            await _fileSegmentSaveService.SaveSegment(segment);
        }

        _logger.LogInformation("Ending peer connection segment processor");
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

    private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out int messageSize, out byte messageId, out ReadOnlySequence<byte> payload)
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
        {
            _logger.LogError("Could not read msg size");
            return false;
        }

        if (messageSize == 0)
        {
            _logger.LogDebug("Keep alive recieved, return true");
            buffer = buffer.Slice(4);
            return true;
        }

        if (!sequenceReader.TryRead(out messageId))
        {
            _logger.LogError("Could not read message Id");
            return false;
        }

        if (messageSize == 1)
        {
            _logger.LogDebug("Received payloadless message");
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
            return true;


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
            default:
                handled = true;
                break;
                ////Return false here when we cannot process the message. It tells the main loop to disconnect and stop comms with this peer
                ////For now we'll return true, but we should change this to false once we've implemented all the message handlers
                //handled = true;
                //break;
        }

        //If the peer bitfield is null, then we can assume that the peer has an empty bitfield
        _state.PeerBitfield ??= new Bitfield(_meta.NumberOfPieces);
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
        var bitfield = new Bitfield(_meta.NumberOfPieces);
        bitfield.Fill(buffer);
        _state.PeerBitfield = bitfield;
        return true;
    }


    //This should be placed into a request channel and processed sequentially in a separate thread
    private async ValueTask<bool> HandleRequestAsync(ReadOnlySequence<byte> payload)
    {
        var pieceRequest = PieceRequestMessage.FromReadOnlySequence(payload);
        await PeerPeieceRequests.Writer.WriteAsync(pieceRequest);
        return true;
    }

    private async ValueTask<bool> HandlePieceAsync(ReadOnlySequence<byte> payload, int chunkSize)
    {
        var segment = PooledPieceSegment.FromReadOnlySequence(payload, chunkSize, _infoHash);
        await PIECE_SEGMENT_CHANNEL.Writer.WriteAsync(segment, _cts.Token);
        BytesDownloaded += segment.Buffer.Length;
        _state.PiecesReceived += 1;
        return true;
    }

    private bool HandleCancel(ref ReadOnlySequence<byte> payload)
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

    private bool HandlePort(ref ReadOnlySequence<byte> payload)
    {
        var reader = new SequenceReader<byte>(payload);
        if (!reader.TryReadBigEndian(out short port))
        {
            _logger.LogError("Error reading piece index value");
            return false;
        }
        return true;
    }


    public async Task SendBitfield(IBitfield bitfield) => await WriteBitfieldAsync(bitfield);
    public async Task SendIntereted() => await SendMessageAsync(PeerWireMessageType.Interested);
    public async Task SendNotInterested() => await SendMessageAsync(PeerWireMessageType.NotInterested);
    public async Task SendChoke() => await SendMessageAsync(PeerWireMessageType.Choke);
    public async Task SendUnchoke() => await SendMessageAsync(PeerWireMessageType.Unchoke);
    public async Task SendHave(int pieceIndex) => await SendMessageAsync(PeerWireMessageType.Have, pieceIndex);
    public async Task SendPieceRequest(int pieceIndex, int begin, int length = PIECE_SEGMENT_REQUEST_SIZE) =>
        await SendMessageAsync(PeerWireMessageType.Request, pieceIndex, begin, length);

    public async Task SendPiece(PreparedPacket pak)
    {
        await OUTBOUND_MESSAGES.Writer.WriteAsync(pak, _cts.Token);
        BytesUploaded += pak.Size - 13;
    }

    public async Task SendCancel(int pieceIndex, int begin, int length) => await SendMessageAsync(PeerWireMessageType.Cancel, pieceIndex, begin, length);

    private async Task SendMessageAsync(byte messageId) => await WritePacketAsync(messageId);
    private async Task SendMessageAsync(byte messageId, int p1) => await WritePacketAsync(messageId, p1);
    private async Task SendMessageAsync(byte messageId, int p1, int p2) => await WritePacketAsync(messageId, p1, p2);
    private async Task SendMessageAsync(byte messageId, int p1, int p2, int p3) => await WritePacketAsync(messageId, p1, p2, p3);

    private async Task WriteBitfieldAsync(IBitfield bitfield) =>
        await OUTBOUND_MESSAGES.Writer.WriteAsync(MessagePacker.Pack(bitfield), _cts.Token);
    private async Task WritePacketAsync(byte messageId) =>
        await OUTBOUND_MESSAGES.Writer.WriteAsync(MessagePacker.Pack(messageId), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1) =>
        await OUTBOUND_MESSAGES.Writer.WriteAsync(MessagePacker.Pack(messageId, p1), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1, int p2) =>
        await OUTBOUND_MESSAGES.Writer.WriteAsync(MessagePacker.Pack(messageId, p1, p2), _cts.Token);
    private async Task WritePacketAsync(byte messageId, int p1, int p2, int p3) =>
        await OUTBOUND_MESSAGES.Writer.WriteAsync(MessagePacker.Pack(messageId, p1, p2, p3), _cts.Token);


    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing peer client {PeerId}", PeerId);
        _cts.Cancel();
        await _connection.DisposeAsync();
    }
}

public readonly record struct PieceRequestMessage(int PieceIndex, int Begin, int Length)
{
    public static PieceRequestMessage FromReadOnlySequence(ReadOnlySequence<byte> payload)
    {
        var reader = new SequenceReader<byte>(payload);
        if (!reader.TryReadBigEndian(out int pieceIndex))
            throw new InvalidDataException("Could not read piece index");
        if (!reader.TryReadBigEndian(out int begin))
            throw new InvalidDataException("Could not read begin index");
        if (!reader.TryReadBigEndian(out int length))
            throw new InvalidDataException("Could not read length");

        return new PieceRequestMessage(pieceIndex, begin, length);
    }
}

public sealed class PreparedPacket : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private readonly byte[] _buffer;
    public readonly int Size;

    public PreparedPacket(int size)
    {
        _pool = ArrayPool<byte>.Shared;
        _buffer = _pool.Rent(size);
        Size = size;
    }

    public void Dispose()
    {
        _pool.Return(_buffer);
    }

    public Span<byte> AsSpan() => _buffer.AsSpan()[..Size];
}
