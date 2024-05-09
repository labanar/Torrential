using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
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

public sealed class PeerWireClient : IDisposable
{
    private const int PIECE_SEGMENT_REQUEST_SIZE = 16384;
    private readonly PeerWireState _state;
    private readonly IPeerWireConnection _connection;
    private readonly ILogger _logger;

    public PeerInfo PeerInfo => _connection.PeerInfo;

    private SemaphoreSlim _pieceRequestLimit = new SemaphoreSlim(10, 10);

    private readonly Channel<PreparedPacket> OUTBOUND_MESSAGES = Channel.CreateBounded<PreparedPacket>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });


    private readonly Channel<PeerPacket> INBOUND_MESSAGES = Channel.CreateBounded<PeerPacket>(new BoundedChannelOptions(5)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<PooledPieceSegment> PIECE_SEGMENT_CHANNEL = Channel.CreateBounded<PooledPieceSegment>(new BoundedChannelOptions(10)
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

    private CancellationTokenSource _processCts;
    private TorrentMetadata _meta;
    private BitfieldManager _bitfields;
    private InfoHash _infoHash;
    private IFileSegmentSaveService _fileSegmentSaveService;
    public PeerWireState State => _state;

    public long BytesUploaded { get; private set; }
    public long BytesDownloaded { get; private set; }
    public PeerId PeerId { get; private set; }

    public DateTimeOffset LastMessageTimestamp { get; private set; }

    public PeerWireClient(IPeerWireConnection connection, ILogger logger)
    {
        if (!connection.PeerId.HasValue)
            throw new ArgumentException("Peer Id must be set", nameof(connection));


        _connection = connection;
        _logger = logger;
        _state = new PeerWireState();
        LastMessageTimestamp = connection.ConnectionTimestamp;
        PeerId = connection.PeerId.Value;
    }


    public async Task Process(TorrentMetadata meta, BitfieldManager bitfields, IFileSegmentSaveService fileSegmentSaveService, CancellationToken cancellationToken)
    {
        _meta = meta;
        _bitfields = bitfields;
        _infoHash = meta.InfoHash;
        _fileSegmentSaveService = fileSegmentSaveService;
        _processCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readerTask = ProcessReads(_processCts.Token);
        var writeTask = ProcessWrites(_processCts.Token);
        var savingTask = ProcessSegments(_processCts.Token);

        try
        {
            await readerTask;
            await writeTask;
            await savingTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing peer connection {PeerId}", PeerId);
        }


        OUTBOUND_MESSAGES.Writer.Complete();
        PIECE_SEGMENT_CHANNEL.Writer.Complete();
        INBOUND_MESSAGES.Writer.Complete();

        //Clear out any disposables stuck in the channels
        await foreach (var pak in OUTBOUND_MESSAGES.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var pak in PIECE_SEGMENT_CHANNEL.Reader.ReadAllAsync())
            pak.Dispose();

        await foreach (var pak in INBOUND_MESSAGES.Reader.ReadAllAsync())
            pak.Dispose();
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        await foreach (var pak in OUTBOUND_MESSAGES.Reader.ReadAllAsync(cancellationToken))
        {
            using (pak)
            {
                _connection.Writer.Write(pak.AsSpan());
                await _connection.Writer.FlushAsync(cancellationToken);
            }
        }
    }

    private async Task ProcessSegments(CancellationToken cancellationToken)
    {
        await foreach (var segment in PIECE_SEGMENT_CHANNEL.Reader.ReadAllAsync(cancellationToken))
        {
            await _fileSegmentSaveService.SaveSegment(segment);
        }
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
                    //_logger.LogInformation("GOT A MESSAGE - {MessageId} {MessageSize}", messageId, messageSize);
                    if (!Process(messageSize, messageId, ref payload))
                    {
                        _logger.LogInformation("Disconnecting from Peer: failed to process message - {MessageId} {MessageSize}", messageId, messageSize);
                        await _connection.Reader.CompleteAsync();
                        _processCts.Cancel();
                        _connection.Dispose();
                        return;
                    }
                }

                // Tell the PipeReader how much of the buffer has been consumed.
                _connection.Reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming.
                if (result.IsCompleted)
                {
                    _logger.LogWarning("No further data from peer");
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                _processCts.Cancel();
                _connection.Dispose();
                return;
            }
            catch (IOException)
            {
                _processCts.Cancel();
                _connection.Dispose();
                return;
            }
            catch (SocketException)
            {
                _processCts.Cancel();
                _connection.Dispose();
                return;
            }
            catch (ObjectDisposedException)
            {
                _processCts.Cancel();
                _connection.Dispose();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from peer");
                _processCts.Cancel();
                _connection.Dispose();
                return;
            }
        }

        _logger.LogInformation("Peer read loop ended, disposing connection and exiting");
        _processCts.Cancel();
        _connection.Dispose();
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

    private bool Process(int messageSize, byte messageId, ref ReadOnlySequence<byte> payload)
    {
        if (messageId == 0 && messageSize == 0)
            return true;

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
            case PeerWireMessageType.Piece:
                HandlePiece(payload, messageSize - 9);
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
            case PeerWireMessageType.Request:
                HandleRequest(payload);
                handled = true;
                break;
            default:
                //Return false here when we cannot process the message. It tells the main loop to disconnect and stop comms with this peer
                //For now we'll return true, but we should change this to false once we've implemented all the message handlers
                handled = true;
                break;
        }

        //If we recieve any event that is not the Bitfield, then we can assume that the peer has an empty bitfield
        _state.PeerBitfield ??= new Bitfield(_meta.NumberOfPieces);
        LastMessageTimestamp = DateTimeOffset.UtcNow;
        return handled;
    }

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
    private bool HandleRequest(ReadOnlySequence<byte> payload)
    {
        var sequenceReader = new SequenceReader<byte>(payload);
        if (!sequenceReader.TryReadBigEndian(out int index))
            return false;
        if (!sequenceReader.TryReadBigEndian(out int begin))
            return false;
        if (!sequenceReader.TryReadBigEndian(out int length))
            return false;


        PeerPeieceRequests.Writer.TryWrite(new(index, begin, length));

        return true;
    }
    private bool HandlePiece(ReadOnlySequence<byte> payload, int chunkSize)
    {
        var reader = new SequenceReader<byte>(payload);
        if (!reader.TryReadBigEndian(out int pieceIndex))
        {
            _logger.LogError("Error reading piece index value");
            return false;
        }
        if (!reader.TryReadBigEndian(out int pieceOffset))
        {
            _logger.LogError("Error reading piece offset value");
            return false;
        }
        if (!reader.TryReadExact(chunkSize, out var segmentSequence))
        {
            _logger.LogError("Error reading piece chunk value");
            return false;
        }

        //_logger.LogInformation("Piece received from peer {Index} {Offset} {Length}", pieceIndex, pieceOffset, PIECE_SEGMENT_REQUEST_SIZE);

        var segment = PooledPieceSegment.FromReadOnlySequence(ref segmentSequence, _infoHash, pieceIndex, pieceOffset);
        PIECE_SEGMENT_CHANNEL.Writer.TryWrite(segment);
        BytesDownloaded += segment.Buffer.Length;
        _state.PiecesReceived += 1;
        _pieceRequestLimit.Release();
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
            _logger.LogError("Error reading piece index value");
            return false;
        }
        return true;
    }

    public async Task SendBitfield(IBitfield bitfield)
    {
        WriteBitfield(bitfield);
    }
    public async Task SendKeepAlive()
    {
        WriteKeepAlive();
    }
    public async Task SendIntereted() => await SendMessageAsync(PeerWireMessageType.Interested);
    public async Task SendNotInterested() => await SendMessageAsync(PeerWireMessageType.NotInterested);
    public async Task SendChoke() => await SendMessageAsync(PeerWireMessageType.Choke);
    public async Task SendUnchoke() => await SendMessageAsync(PeerWireMessageType.Unchoke);
    public async Task SendHave(int pieceIndex) => await SendMessageAsync(PeerWireMessageType.Have, pieceIndex);
    public async Task SendPieceRequest(int pieceIndex, int begin, int length = PIECE_SEGMENT_REQUEST_SIZE)
    {
        await _pieceRequestLimit.WaitAsync(_processCts.Token);
        await SendMessageAsync(PeerWireMessageType.Request, pieceIndex, begin, length);
    }

    public void SendPiece(int pieceIndex, int begin, ReadOnlySpan<byte> payload)
    {
        var pak = new PreparedPacket(13 + payload.Length);
        Span<byte> buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(9 + payload.Length);
        buffer[4] = PeerWireMessageType.Piece;
        buffer[5..].TryWriteBigEndian(pieceIndex);
        buffer[9..].TryWriteBigEndian(begin);
        payload.CopyTo(buffer.Slice(13));
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
        BytesUploaded += payload.Length;
    }

    public async Task SendCancel(int pieceIndex, int begin, int length) => await SendMessageAsync(PeerWireMessageType.Cancel, pieceIndex, begin, length);
    private async Task SendMessageAsync(byte messageId)
    {
        WritePacket(messageId);
    }
    private async Task SendMessageAsync(byte messageId, int p1)
    {
        WritePacket(messageId, p1);
    }
    private async Task SendMessageAsync(byte messageId, int p1, int p2)
    {
        WritePacket(messageId, p1, p2);
    }
    private async Task SendMessageAsync(byte messageId, int p1, int p2, int p3)
    {
        WritePacket(messageId, p1, p2, p3);
    }

    private void SendMessage(byte messageId)
    {
        WritePacket(messageId);
    }
    private void SendMessage(byte messageId, int p1)
    {
        WritePacket(messageId, p1);
    }
    private void SendMessage(byte messageId, int p1, int p2)
    {
        WritePacket(messageId, p1, p2);
    }
    private void SendMessage(byte messageId, int p1, int p2, int p3)
    {
        WritePacket(messageId, p1, p2, p3);
    }
    private void SendMessage(byte messageId, Span<byte> payload)
    {
        WritePacket(messageId, payload);
    }

    private void WriteBitfield(IBitfield bitfield)
    {
        var pak = new PreparedPacket(bitfield.Bytes.Length + 5);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(bitfield.Bytes.Length + 1);
        buffer[4] = PeerWireMessageType.Bitfield;
        bitfield.Bytes.CopyTo(buffer[5..]);
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }
    private void WriteKeepAlive()
    {
        var pak = new PreparedPacket(4);
        var buffer = pak.AsSpan();
        buffer[0] = 0;
        buffer[1] = 0;
        buffer[2] = 0;
        buffer[3] = 0;
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }
    private void WritePacket(byte messageId)
    {
        var pak = new PreparedPacket(5);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(1);
        buffer[4] = messageId;
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }
    private void WritePacket(byte messageId, int p1)
    {
        var pak = new PreparedPacket(9);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(5);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }
    private void WritePacket(byte messageId, int p1, int p2)
    {
        var pak = new PreparedPacket(13);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(9);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        buffer[9..].TryWriteBigEndian(p2);
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }
    private void WritePacket(byte messageId, int p1, int p2, int p3)
    {
        var pak = new PreparedPacket(17);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(13);
        buffer[4] = messageId;
        buffer[5..].TryWriteBigEndian(p1);
        buffer[9..].TryWriteBigEndian(p2);
        buffer[13..].TryWriteBigEndian(p3);
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }

    private void WritePacket(byte messageId, Span<byte> payload)
    {
        var pak = new PreparedPacket(5 + payload.Length);
        var buffer = pak.AsSpan();
        buffer.TryWriteBigEndian(payload.Length + 1);
        buffer[4] = messageId;
        payload.CopyTo(buffer.Slice(5, payload.Length));
        OUTBOUND_MESSAGES.Writer.TryWrite(pak);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

public readonly record struct PieceRequestMessage(int PieceIndex, int Begin, int Length);

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

public sealed class PeerPacket : IDisposable
{
    public byte MessageId => _messageId;
    public ReadOnlySpan<byte> Payload => _buffer.AsSpan().Slice(0, _length);

    private readonly byte _messageId;
    private readonly byte[]? _buffer;
    private readonly int _length;

    public PeerPacket(byte messageId, int payloadLength)
    {
        _messageId = messageId;
        _length = payloadLength;
        if (payloadLength > 0)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(payloadLength);
        }
    }

    public void Fill(ReadOnlySequence<byte> payload)
    {
        payload.CopyTo(_buffer);
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);

        }
    }
}