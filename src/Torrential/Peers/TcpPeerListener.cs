using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Torrential.Settings;

namespace Torrential.Peers;

public sealed class TcpPeerListenerBackgroundService(HandshakeService handshakeService, PeerSwarm peerSwarm, SettingsManager settingsManager, ILogger<PeerWireConnection> pwcLogger, ILogger<TcpPeerListenerBackgroundService> logger)
    : BackgroundService
{
    private readonly Channel<Socket> _halfOpenConnections = Channel.CreateBounded<Socket>(new BoundedChannelOptions(50)
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly Channel<Task> _connectionTasks = Channel.CreateBounded<Task>(new BoundedChannelOptions(50)
    {
        SingleReader = true,
        SingleWriter = false
    });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var acceptConnectionsTask = AcceptConnections(stoppingToken);
        var processHalfOpenConnectionsTask = ProcessHalfOpenConnections(stoppingToken);
        var cleanupTask = CleanupHalfOpenTasks(stoppingToken);
        await Task.WhenAll(acceptConnectionsTask, processHalfOpenConnectionsTask, cleanupTask);
    }

    private async Task AcceptConnections(CancellationToken stoppingToken)
    {
        TcpListener? tcpListener = null;
        int? port = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var newPort = await WaitForEnabled(stoppingToken);
            if (!port.HasValue || newPort != port)
            {
                tcpListener?.Stop();
                tcpListener?.Dispose();
                tcpListener = new TcpListener(IPAddress.Any, newPort);
                tcpListener.Start();
                logger.LogInformation("TCP Listener started on {Port}", port);
                port = newPort;
            }

            var socket = await tcpListener!.AcceptSocketAsync();
            if (socket == null) continue;
            if (!socket.Connected)
            {
                socket.Dispose();
                continue;
            }
            await _halfOpenConnections.Writer.WriteAsync(socket, stoppingToken);
        }
    }

    private async Task ProcessHalfOpenConnections(CancellationToken stoppingToken)
    {
        await foreach (var client in _halfOpenConnections.Reader.ReadAllAsync(stoppingToken))
        {
            await _connectionTasks.Writer.WaitToWriteAsync(stoppingToken);
            await _connectionTasks.Writer.WriteAsync(ConnectToPeer(client, stoppingToken), stoppingToken);
        }
    }

    private async Task CleanupHalfOpenTasks(CancellationToken stoppingToken)
    {
        await foreach (var task in _connectionTasks.Reader.ReadAllAsync(stoppingToken))
        {
            await task;
        }
    }

    private async Task ConnectToPeer(Socket socket, CancellationToken stoppingToken)
    {
        try
        {
            var timeOutToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeOutToken.CancelAfter(TimeSpan.FromSeconds(10));

            var conn = new PeerWireSocketConnection(socket, handshakeService, pwcLogger);
            logger.LogInformation("TCP client connected to listener");
            var connectionResult = await conn.ConnectInbound(timeOutToken.Token);

            if (!connectionResult.Success)
            {
                logger.LogWarning("Peer connection failed");
                conn.Dispose();
            }

            logger.LogInformation("Peer connected: {PeerId}", conn.PeerId);
            await peerSwarm.AddToSwarm(conn);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handshaking with peer");
            socket.Dispose();
        }
    }

    private async ValueTask<int> WaitForEnabled(CancellationToken stoppingToken)
    {
        var tcpSettings = await settingsManager.GetTcpListenerSettings();
        while (!stoppingToken.IsCancellationRequested && !tcpSettings.Enabled)
        {
            await Task.Delay(1000, stoppingToken);
        }

        return tcpSettings.Port;
    }
}
