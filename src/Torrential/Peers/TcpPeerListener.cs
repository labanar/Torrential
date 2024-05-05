﻿using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using Torrential.Torrents;

namespace Torrential.Peers;

public sealed class TcpPeerListener
{
    //private readonly IPAddress _listenerAddress = IPAddress.Loopback;
    private readonly int _listenerPort = 53123;
    private readonly IPEndPoint _endpoint = new(IPAddress.Any, 53123);
    private readonly TorrentMetadataCache _metaCache;
    private readonly IPeerService _peerService;
    private readonly HandshakeService _handshakeService;
    private readonly ILogger<PeerWireConnection> _pwcLogger;
    private readonly ILogger<TcpPeerListener> _logger;
    private readonly PeerSwarm _peerSwarm;
    private readonly TcpListener _tcpListener;
    public int Port => _listenerPort;

    public TcpPeerListener(HandshakeService handshakeService, ILogger<PeerWireConnection> pwcLogger, PeerSwarm peerSwarm, ILogger<TcpPeerListener> logger)
    {
        _handshakeService = handshakeService;
        _pwcLogger = pwcLogger;
        _logger = logger;
        _peerSwarm = peerSwarm;
        _tcpListener = new TcpListener(_endpoint);
    }

    public async Task Start(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting TCP listener");
        _tcpListener.Start();
        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await _tcpListener.AcceptTcpClientAsync();
            if (client == null) continue;
            if (!client.Connected) continue;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _ = Task.Run(async () =>
            {
                var conn = new PeerWireConnection(_handshakeService, client, _pwcLogger);
                _logger.LogInformation("TCP client connected to listener");
                var connectionResult = await conn.ConnectInbound(CancellationToken.None);

                if (!connectionResult.Success)
                {
                    _logger.LogWarning("Peer connection failed");
                    conn.Dispose();
                }

                _logger.LogInformation("Peer connected: {PeerId}", conn.PeerId);
                await _peerSwarm.AddToSwarm(conn);
            });
        }
    }


    public void Stop()
    {
        _tcpListener.Stop();
    }
}