using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Torrential.Settings;

namespace Torrential.Peers;

public sealed class TcpPeerListenerBackgroundService(
    PeerConnectionManager connectionManager,
    SettingsManager settingsManager,
    ILogger<IPeerWireConnection> pwcLogger,
    ILogger<TcpPeerListenerBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await AcceptConnections(stoppingToken);
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

            var connection = new PeerWireSocketConnection(socket, pwcLogger);
            await connectionManager.QueueInboundConnection(connection);
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
