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
    private readonly Channel<TcpClient> _halfOpenConnections = Channel.CreateBounded<TcpClient>(new BoundedChannelOptions(50)
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
        var port = await WaitForEnabled(stoppingToken);
        var tcpListener = new TcpListener(IPAddress.Any, port);
        tcpListener.Start();
        logger.LogInformation("TCP Listener Service Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await tcpListener.AcceptTcpClientAsync();
            if (client == null) continue;
            if (!client.Connected) continue;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            await _halfOpenConnections.Writer.WriteAsync(client, stoppingToken);

            var newPort = await WaitForEnabled(stoppingToken);
            if (newPort != port)
            {
                logger.LogInformation("TCP Listener port changed from {OldPort} to {NewPort}", port, newPort);
                tcpListener.Stop();
                tcpListener = new TcpListener(IPAddress.Any, newPort);
                tcpListener.Start();
                port = newPort;
            }
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



    private async Task ConnectToPeer(TcpClient client, CancellationToken stoppingToken)
    {
        var timeOutToken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeOutToken.CancelAfter(TimeSpan.FromSeconds(10));

        var conn = new PeerWireConnection(handshakeService, client, pwcLogger);
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

    private async Task<int> WaitForEnabled(CancellationToken stoppingToken)
    {
        var tcpSettings = await settingsManager.GetTcpListenerSettings();
        while (!stoppingToken.IsCancellationRequested && !tcpSettings.Enabled)
        {
            await Task.Delay(1000, stoppingToken);
        }

        return tcpSettings.Port;
    }
}
