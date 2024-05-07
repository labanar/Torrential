using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireSocketConnection : IPeerWireConnection
{

    private readonly Socket _socket;
    private readonly HandshakeService _handshakeService;
    private readonly TorrentMetadataCache _metaCache;
    private readonly IPeerService _peerService;
    private readonly Pipe _socketReaderPipe;
    private readonly Pipe _socketWriterPipe;
    private readonly ILogger _logger;
    private Task _socketReaderTask;
    private Task _socketWriterTask;

    public Guid Id { get; }

    public PeerInfo PeerInfo { get; private set; }

    public PeerId? PeerId { get; private set; }

    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }

    public PipeReader Reader => _socketReaderPipe.Reader;

    public PipeWriter Writer => _socketWriterPipe.Writer;

    public DateTimeOffset ConnectionTimestamp { get; private set; } = DateTimeOffset.UtcNow;


    public PeerWireSocketConnection(Socket socket, HandshakeService handshakeService, ILogger logger)
    {
        Id = Guid.NewGuid();
        _socket = socket;
        _handshakeService = handshakeService;
        _socketReaderPipe = new Pipe();
        _socketWriterPipe = new Pipe();
        _logger = logger;
    }

    public async Task<PeerConnectionResult> ConnectInbound(CancellationToken cancellationToken)
    {
        //Assume the socket is connected at this point
        _socketReaderTask = FillReaderPipeAsync(_socket, _socketReaderPipe.Writer);
        _socketWriterTask = FlushWriterPipeAsync(_socket, _socketWriterPipe.Reader);

        var peerHandshake = await _handshakeService.HandleInbound(Writer, Reader, cancellationToken);
        if (!peerHandshake.Success)
        {
            _socket.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = new PeerInfo
        {
            Ip = ((IPEndPoint)_socket.RemoteEndPoint).Address,
            Port = ((IPEndPoint)_socket.RemoteEndPoint).Port
        };
        PeerId = peerHandshake.PeerId;
        InfoHash = peerHandshake.InfoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(peerHandshake);

    }

    public async Task<PeerConnectionResult> ConnectOutbound(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken)
    {
        _socketReaderTask = FillReaderPipeAsync(_socket, _socketReaderPipe.Writer);
        _socketWriterTask = FlushWriterPipeAsync(_socket, _socketWriterPipe.Reader);

        var handshakeResult = await _handshakeService.HandleOutbound(Writer, Reader, infoHash, cancellationToken);
        if (!handshakeResult.Success)
        {
            _socket.Dispose();
            return PeerConnectionResult.Failure;
        }

        PeerInfo = peer;
        PeerId = handshakeResult.PeerId;
        InfoHash = infoHash;
        IsConnected = true;
        ConnectionTimestamp = DateTimeOffset.UtcNow;
        return PeerConnectionResult.FromHandshake(handshakeResult);
    }

    public void Dispose()
    {
        _socket.Dispose();
    }

    private async Task FlushWriterPipeAsync(Socket socket, PipeReader reader)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            foreach (var memory in buffer)
            {
                try
                {
                    await socket.SendAsync(memory, SocketFlags.Partial);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write to socket");
                    return;
                }
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        await reader.CompleteAsync();
    }

    private async Task FillReaderPipeAsync(Socket socket, PipeWriter writer)
    {
        const int minimumBufferSize = 512;

        while (true)
        {
            // Allocate at least 512 bytes from the PipeWriter.
            Memory<byte> memory = writer.GetMemory(minimumBufferSize);
            try
            {
                int bytesRead = await socket.ReceiveAsync(memory);
                if (bytesRead == 0)
                {
                    await Task.Delay(50);
                    continue;
                    //break;
                }
                // Tell the PipeWriter how much was read from the Socket.
                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read from socket and write into pipe");
                break;
            }

            // Make the data available to the PipeReader.
            FlushResult result = await writer.FlushAsync();
            if (result.IsCompleted)
            {
                break;
            }
        }

        // By completing PipeWriter, tell the PipeReader that there's no more data coming.
        await writer.CompleteAsync();
    }

}
