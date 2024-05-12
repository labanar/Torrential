using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net.Sockets;
using Torrential.Trackers;
using Torrential.Utilities;

namespace Torrential.Peers;

public class PeerWireSocketConnection : IPeerWireConnection
{
    private readonly Socket _socket;
    private readonly Pipe _ingressPipe;
    private readonly Task _ingressFillTask;
    private readonly Pipe _egressPipe;
    private readonly Task _egressFillTask;
    private readonly HandshakeService _handshakeService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;

    public Guid Id { get; }



    public PeerInfo? PeerInfo { get; private set; }
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
        _cts = new CancellationTokenSource();


        //Data flow from the socket to the pipe
        _ingressPipe = new Pipe();
        _ingressFillTask = IngressFillPipeAsync(_socket, _ingressPipe.Writer, _cts.Token);
        Reader = _ingressPipe.Reader;

        //Data flow from the pipe to the socket
        _egressPipe = new Pipe();
        _egressFillTask = EgressFillSocketAsync(_socket, _egressPipe.Reader, _cts.Token);
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

        PeerInfo = _socket.GetPeerInfo();
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
    async Task IngressFillPipeAsync(Socket socket, PipeWriter writer, CancellationToken stoppingToken)
    {
        const int minimumBufferSize = 16384;

        while (!stoppingToken.IsCancellationRequested)
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


    async Task EgressFillSocketAsync(Socket socket, PipeReader reader, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {

            ReadResult result = await reader.ReadAsync();
            var buffer = result.Buffer;

            try
            {
                foreach (var segment in buffer)
                {
                    await socket.SendAsync(segment, SocketFlags.None);
                }

                reader.AdvanceTo(buffer.End);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to peer");
                break;
            }

            if (result.IsCompleted)
            {
                break;
            }
        }

        reader.Complete();
    }


    public void Dispose()
    {
        _logger.LogInformation("Disposing connection {Id}", Id);
        _cts.Cancel();
        _socket.Dispose();
    }
}
