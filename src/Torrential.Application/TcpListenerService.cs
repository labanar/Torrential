using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Torrential.Core;

namespace Torrential.Application;

public sealed class TcpListenerService(
    IInboundConnectionHandler connectionHandler,
    ITcpListenerSettings settings,
    ILogger<IPeerWireConnection> pwcLogger,
    ILogger<TcpListenerService> logger)
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
                var socket = await tcpListener!.AcceptSocketAsync(stoppingToken);
                if (socket == null) continue;

                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                if (!socket.Connected)
                {
                    socket.Dispose();
                    continue;
                }

                var connection = new PeerWireSocketConnection(socket, pwcLogger);
                if (await connectionHandler.IsBlockedAsync(connection))
                {
                    logger.LogInformation("Disposing blocked connection");
                    await connection.DisposeAsync();
                    continue;
                }

                await connectionHandler.HandleAsync(connection);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error accepting connection");
            }
        }

        tcpListener?.Stop();
        tcpListener?.Dispose();
    }

    private async ValueTask<int> WaitForEnabled(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !settings.Enabled)
        {
            await Task.Delay(1000, stoppingToken);
        }

        return settings.Port;
    }
}
