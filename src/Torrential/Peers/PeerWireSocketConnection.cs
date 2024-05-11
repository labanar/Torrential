using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Torrential.Torrents;
using Torrential.Trackers;

namespace Torrential.Peers;

public class PeerWireSocketConnection : IPeerWireConnection
{
    private readonly Socket _socket;
    private readonly Pipe _ingressPipe;
    private readonly Task _ingressFillTask;
    private readonly Pipe _egressPipe;
    private readonly Task _egressFillTask;
    private readonly NetworkStream _stream;
    private readonly HandshakeService _handshakeService;
    private readonly ILogger _logger;
    private readonly TorrentMetadataCache _metaCache;

    public Guid Id { get; }

    public PeerInfo PeerInfo { get; private set; }

    public PeerId? PeerId { get; private set; }

    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public bool IsConnected { get; private set; }

    public DateTimeOffset ConnectionTimestamp { get; private set; } = DateTimeOffset.UtcNow;

    public PipeReader Reader { get; }
    public PipeWriter Writer { get; }

    public PeerWireSocketConnection(Socket socket, HandshakeService handshakeService, ILogger logger)
    {
        Id = Guid.NewGuid();
        _socket = socket;
        _handshakeService = handshakeService;
        _logger = logger;


        //Data flow from the socket to the pipe
        _ingressPipe = new Pipe();
        _ingressFillTask = IngressFillPipeAsync(_socket, _ingressPipe.Writer);
        Reader = _ingressPipe.Reader;

        //Data flow from the pipe to the socket
        _egressPipe = new Pipe();
        _egressFillTask = EgressFillSocketAsync(_socket, _egressPipe.Reader);
        Writer = _egressPipe.Writer;
    }

    public async Task<PeerConnectionResult> ConnectInbound(CancellationToken cancellationToken)
    {
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


    //Read from the socket and write to the pipe
    //This pipe will the be read by the PeerWireClient to process the data coming from the peer
    async Task IngressFillPipeAsync(Socket socket, PipeWriter writer)
    {
        const int minimumBufferSize = 16384;

        while (true)
        {
            // Allocate at least 512 bytes from the PipeWriter
            Memory<byte> memory = writer.GetMemory(minimumBufferSize);
            try
            {
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                if (bytesRead == 0)
                {
                    break;
                }
                // Tell the PipeWriter how much was read from the Socket
                writer.Advance(bytesRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from peer");
                break;
            }

            // Make the data available to the PipeReader
            FlushResult result = await writer.FlushAsync();

            if (result.IsCompleted)
            {
                break;
            }
        }

        // Tell the PipeReader that there's no more data coming
        writer.Complete();
    }


    async Task EgressFillSocketAsync(Socket socket, PipeReader reader)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            var buffer = result.Buffer;

            foreach (var segment in buffer)
            {
                await socket.SendAsync(segment, SocketFlags.None);
            }

            reader.AdvanceTo(buffer.End);

            if (result.IsCompleted)
            {
                break;
            }
        }

        reader.Complete();
    }

    ////Read from the pipe and 
    //async Task IngressReadPipeAsync(PipeReader reader)
    //{
    //    while (true)
    //    {
    //        ReadResult result = await reader.ReadAsync();

    //        ReadOnlySequence<byte> buffer = result.Buffer;
    //        SequencePosition? position = null;

    //        do
    //        {
    //            // Look for a EOL in the buffer
    //            position = buffer.PositionOf((byte)'\n');

    //            if (position != null)
    //            {
    //                // Process the line
    //                ProcessLine(buffer.Slice(0, position.Value));

    //                // Skip the line + the \n character (basically position)
    //                buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
    //            }
    //        }
    //        while (position != null);

    //        // Tell the PipeReader how much of the buffer we have consumed
    //        reader.AdvanceTo(buffer.Start, buffer.End);

    //        // Stop reading if there's no more data coming
    //        if (result.IsCompleted)
    //        {
    //            break;
    //        }
    //    }

    //    // Mark the PipeReader as complete
    //    reader.Complete();
    //}


    public void Dispose()
    {
        _socket.Dispose();
    }
}
