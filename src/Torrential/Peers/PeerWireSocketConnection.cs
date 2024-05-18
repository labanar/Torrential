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


    public PeerWireSocketConnection(Socket socket, ILogger logger)
    {
        Id = Guid.NewGuid();
        _socket = socket;
        _logger = logger;
        _cts = new CancellationTokenSource();


        //Data flow from the socket to the pipe
        _ingressPipe = PipePool.Shared.Get();
        _ingressFillTask = IngressFillPipeAsync(_socket, _ingressPipe.Writer, _cts.Token);
        Reader = _ingressPipe.Reader;

        //Data flow from the pipe to the socket
        _egressPipe = PipePool.Shared.Get();
        _egressFillTask = EgressFillSocketAsync(_socket, _egressPipe.Reader, _cts.Token);
        Writer = _egressPipe.Writer;
        PeerInfo = socket.GetPeerInfo();
    }

    public void SetInfoHash(InfoHash infoHash)
    {
        if (InfoHash != InfoHash.None)
            throw new InvalidOperationException("InfoHash already set");

        InfoHash = infoHash;
        PeerMetrics.IncrementConnectedPeersCount(InfoHash);
    }

    public void SetPeerId(PeerId peerId)
    {
        PeerId = peerId;
    }

    //Read from the socket and write to the pipe
    //This pipe will the be read by the PeerWireClient to process the data coming from the peer
    async Task IngressFillPipeAsync(Socket socket, PipeWriter writer, CancellationToken stoppingToken)
    {
        const int minimumBufferSize = 512;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Allocate at least 512 bytes from the PipeWriter
            try
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, stoppingToken);
                PeerMetrics.IncrementIngressBytes(InfoHash, bytesRead);
                if (bytesRead == 0)
                {
                    break;
                }
                // Tell the PipeWriter how much was read from the Socket
                writer.Advance(bytesRead);

                // Make the data available to the PipeReader
                FlushResult result = await writer.FlushAsync(stoppingToken);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from peer");
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
            try
            {
                ReadResult result = await reader.ReadAsync(stoppingToken);
                var buffer = result.Buffer;

                foreach (var block in buffer)
                {
                    var bytesWritten = await socket.SendAsync(block, SocketFlags.None, stoppingToken);
                    PeerMetrics.IncrementEgressBytes(InfoHash, bytesWritten);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing to peer");
                break;
            }
        }

        reader.Complete();
    }


    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        await Task.WhenAll(_ingressFillTask, _egressFillTask);
        _egressPipe.Reader.Complete();
        _egressPipe.Writer.Complete();
        _ingressPipe.Reader.Complete();
        _ingressPipe.Writer.Complete();

        PipePool.Shared.Return(_ingressPipe);
        PipePool.Shared.Return(_egressPipe);

        _socket.Dispose();
        _logger.LogDebug("Disposing connection {Id}", Id);


        if (InfoHash != InfoHash.None)
            PeerMetrics.DecrementConnectedPeersCount(InfoHash);
    }
}
