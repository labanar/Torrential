using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Torrential.Settings;

namespace Torrential.Peers;

public sealed class TcpPeerListener(HandshakeService handshakeService, PeerSwarm peerSwarm, SettingsManager settingsManager, ILogger<PeerWireConnection> pwcLogger, ILogger<TcpPeerListener> logger)
{
    private TcpListener? _tcpListener;

    public async Task Start(CancellationToken stoppingToken)
    {
        var port = await WaitForEnabled(stoppingToken);
        _tcpListener = new TcpListener(IPAddress.Any, port);
        _tcpListener.Start();
        logger.LogInformation("TCP Listener Service Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await _tcpListener.AcceptTcpClientAsync();
            if (client == null) continue;
            if (!client.Connected) continue;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _ = Task.Run(async () =>
            {
                var conn = new PeerWireConnection(handshakeService, client, pwcLogger);
                logger.LogInformation("TCP client connected to listener");
                var connectionResult = await conn.ConnectInbound(CancellationToken.None);

                if (!connectionResult.Success)
                {
                    logger.LogWarning("Peer connection failed");
                    conn.Dispose();
                }

                logger.LogInformation("Peer connected: {PeerId}", conn.PeerId);
                await peerSwarm.AddToSwarm(conn);
            });

            var newPort = await WaitForEnabled(stoppingToken);
            if (newPort != port)
            {
                logger.LogInformation("TCP Listener port changed from {OldPort} to {NewPort}", port, newPort);
                _tcpListener.Stop();
                _tcpListener = new TcpListener(IPAddress.Any, newPort);
                _tcpListener.Start();
                port = newPort;
            }
        }
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


    public void Stop()
    {
        _tcpListener.Stop();
    }
}
