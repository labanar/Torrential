using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Torrential.Core;

public class PeerWireSocketConnection : IPeerWireConnection
{
    private readonly Socket _socket;
    private readonly Pipe _ingressPipe;
    private readonly Task _ingressFillTask;
    private readonly Pipe _egressPipe;
    private readonly Task _egressFillTask;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Action<InfoHash, int>? _onIngressBytes;
    private readonly Action<InfoHash, int>? _onEgressBytes;

    public Guid Id { get; }
    public PeerInfo PeerInfo { get; }
    public PeerId? PeerId { get; private set; }
    public InfoHash InfoHash { get; private set; } = InfoHash.None;
    public DateTimeOffset ConnectionTimestamp { get; }
    public PipeReader Reader { get; }
    public PipeWriter Writer { get; }

    public PeerWireSocketConnection(
        Socket socket,
        ILogger logger,
        Action<InfoHash, int>? onIngressBytes = null,
        Action<InfoHash, int>? onEgressBytes = null)
    {
        Id = Guid.NewGuid();
        _socket = socket;
        _logger = logger;
        _onIngressBytes = onIngressBytes;
        _onEgressBytes = onEgressBytes;
        _cts = new CancellationTokenSource();

        // Data flow from the socket to the pipe
        _ingressPipe = PipePool.Shared.Get();
        _ingressFillTask = IngressFillPipeAsync(_socket, _ingressPipe.Writer, _cts.Token);
        Reader = _ingressPipe.Reader;

        // Data flow from the pipe to the socket
        _egressPipe = PipePool.Shared.Get();
        _egressFillTask = EgressFillSocketAsync(_socket, _egressPipe.Reader, _cts.Token);
        Writer = _egressPipe.Writer;

        PeerInfo = GetPeerInfo(socket);
        ConnectionTimestamp = DateTimeOffset.UtcNow;
    }

    public void SetInfoHash(InfoHash infoHash)
    {
        if (InfoHash != InfoHash.None)
            throw new InvalidOperationException("InfoHash already set");

        InfoHash = infoHash;
    }

    public void SetPeerId(PeerId peerId)
    {
        PeerId = peerId;
    }

    private async Task IngressFillPipeAsync(Socket socket, PipeWriter writer, CancellationToken stoppingToken)
    {
        const int minimumBufferSize = 512;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                int bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, stoppingToken);
                _onIngressBytes?.Invoke(InfoHash, bytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                writer.Advance(bytesRead);
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

        writer.Complete();
    }

    private async Task EgressFillSocketAsync(Socket socket, PipeReader reader, CancellationToken stoppingToken)
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
                    _onEgressBytes?.Invoke(InfoHash, bytesWritten);
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

    private static PeerInfo GetPeerInfo(Socket socket)
    {
        var ipEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
        return new PeerInfo(ipEndPoint.Address, ipEndPoint.Port);
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
    }
}
