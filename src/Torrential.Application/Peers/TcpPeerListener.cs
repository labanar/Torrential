using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Torrential.Application.Settings;
using Torrential.Application.Utilities;

namespace Torrential.Application.Peers;

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
                tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                tcpListener.Start();
                port = newPort;
                logger.LogInformation("TCP Listener started on {Port}", port);
            }

            try
            {
                var socket = await tcpListener!.AcceptSocketAsync();

                if (await connectionManager.IsBlockedConnection(socket.GetPeerInfo().Ip))
                {
                    logger.LogInformation("Disposing blocked connection");
                    socket.Dispose();
                    continue;
                }

                if (socket == null) continue;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                if (!socket.Connected)
                {
                    socket.Dispose();
                    continue;
                }

                var connection = new PeerWireSocketConnection(socket, pwcLogger);
                await connectionManager.QueueInboundConnection(connection);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accepting connection");
            }
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
