using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Channels;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Peers;

public sealed class PeerWireState
{
    public DateTimeOffset LastChokedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool AmChoked { get; set; } = true;
    public bool PeerChoked { get; set; } = true;
    public bool AmInterested { get; set; } = false;
    public bool PeerInterested { get; set; } = false;
    public int PiecesReceived { get; set; } = 0;
    public Bitfield? Bitfield { get; set; } = null;
    public DateTimeOffset PeerLastInterestedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PeerWireClient : IDisposable
{
    private const int PIECE_SEGMENT_REQUEST_SIZE = 16384;
    private readonly PeerWireState _state;
    private readonly IPeerWireConnection _connection;
    private readonly ILogger _logger;


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


    private readonly Channel<PooledPieceSegment> PIECE_SEGMENT_CHANNEL = Channel.CreateUnbounded<PooledPieceSegment>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        SingleWriter = true
    });

    private CancellationTokenSource _processCts;
    private BitfieldManager _bitfields;
    private InfoHash _infoHash;
    private IFileSegmentSaveService _fileSegmentSaveService;

    public PeerWireState State => _state;

    public PeerWireClient(IPeerWireConnection connection, ILogger logger)
    {
        _connection = connection;
        _logger = logger;
        _state = new PeerWireState();
    }

    private async Task ProcessWrites(CancellationToken cancellationToken)
    {
        await foreach (var pak in OUTBOUND_MESSAGES.Reader.ReadAllAsync(cancellationToken))
        {
            using (pak)
            {
                _connection.Writer.Write(pak.AsSpan());
                await _connection.Writer.FlushAsync();
            }
        }
    }
    public async Task Process(TorrentMetadata meta, BitfieldManager bitfields, IFileSegmentSaveService fileSegmentSaveService, CancellationToken cancellationToken)
    {
        _bitfields = bitfields;
        _infoHash = meta.InfoHash;
        _fileSegmentSaveService = fileSegmentSaveService;
        _state.Bitfield = new Bitfield(meta.NumberOfPieces);
        _processCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readerTask = ProcessReads(_processCts.Token);
        var writeTask = ProcessWrites(_processCts.Token);
        var savingTask = ProcessSegments(_processCts.Token);
        await readerTask;
        await writeTask;
        await savingTask;
    }

    private async Task ProcessSegments(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach (var segment in PIECE_SEGMENT_CHANNEL.Reader.ReadAllAsync(cancellationToken))
            {
                using (segment)
                {
                    _fileSegmentSaveService.SaveSegment(segment);
                }
            }
        }
    }

    private async Task ProcessReads(CancellationToken cancellationToken)
    {
        //TODO - enforce global and peer rate limits here
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ReadResult result = await _connection.Reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (TryReadMessage(ref buffer, out var messageSize, out var messageId, out ReadOnlySequence<byte> payload))
                {
                    _logger.LogInformation("GOT A MESSAGE - {MessageId} {MessageSize}", messageId, messageSize);
                    if (!Process(messageSize, messageId, ref payload))
                    {
                        _logger.LogInformation("Disconnecting from Peer: failed to process message - {MessageId} {MessageSize}", messageId, messageSize);
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
                _connection.Dispose();
                return;
            }
            catch (SocketException)
            {
                _connection.Dispose();
                return;
            }
            catch
            {
                throw;
            }
        }

        await _connection.Reader.CompleteAsync();
        _processCts.Cancel();
        _connection.Dispose();
        return;
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

        switch (messageId)
        {
            case PeerWireMessageType.Choke:
                HandleChoke();
                return true;
            case PeerWireMessageType.Unchoke:
                HandleUnchoke();
                return true;
            case PeerWireMessageType.Interested:
                HandleInterested();
                return true;
            case PeerWireMessageType.NotInterested:
                HandleNotInterested();
                return true;
            case PeerWireMessageType.Piece:
                HandlePiece(payload);
                return true;
            case PeerWireMessageType.Bitfield:
                HandleBitfield(payload);
                return true;
        }


        //Return false here when we cannot process the message. It tells the main loop to disconnect and stop comms with this peer

        return true;

        //Put this into a generic packet format
        //var packet = new PeerPacket(messageId, messageSize - 1);
        //if (messageSize > 1)
        //    packet.Fill(payload);

        ////Place this in the reader channel
        //INBOUND_MESSAGES.Writer.TryWrite(packet);
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

        _state.Bitfield?.MarkHave(index);
        return true;
    }
    private bool HandleBitfield(ReadOnlySequence<byte> payload)
    {
        Span<byte> buffer = stackalloc byte[(int)payload.Length];
        payload.CopyTo(buffer);
        _state.Bitfield = new Bitfield(buffer);
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

        return true;
    }
    private bool HandlePiece(ReadOnlySequence<byte> payload)
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
        if (!reader.TryReadExact(PIECE_SEGMENT_REQUEST_SIZE, out var segmentSequence))
        {
            _logger.LogError("Error reading piece offset value");
            return false;
        }

        _logger.LogInformation("Piece received from peer {Index} {Offset} {Length}", pieceIndex, pieceOffset, PIECE_SEGMENT_REQUEST_SIZE);

        var segment = PooledPieceSegment.FromReadOnlySequence(ref segmentSequence, _infoHash, pieceIndex, pieceOffset);
        PIECE_SEGMENT_CHANNEL.Writer.TryWrite(segment);
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

    public async Task SendBitfield(Bitfield bitfield)
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

    private void WriteBitfield(Bitfield bitfield)
    {
        //var pak = new PreparedPacket(bitfield..Length + 5);
        //var buffer = pak.AsSpan();
        //buffer.TryWriteBigEndian(bitfield.Value.Length);
        //buffer[4] = PeerWireMessageType.Bitfield;
        //bitfield.Value.CopyTo(buffer[5..]);
        //OUTBOUND_MESSAGES.Writer.TryWrite(pak);
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