using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public sealed class PeerConnectionService(
    ITorrentManager torrentManager,
    IPeerPool peerPool,
    HandshakeService handshakeService,
    ILogger<PeerConnectionService> logger,
    ILogger<IPeerWireConnection> pwcLogger) : BackgroundService, IPeerConnectionManager
{
    private readonly PeerId _selfId = PeerId.New;
    private readonly ConcurrentDictionary<InfoHash, ConcurrentDictionary<PeerInfo, ManagedPeerConnection>> _connections = new();
    private readonly SemaphoreSlim _halfOpenSemaphore = new(50, 50);
    private const int MaxPeersPerTorrent = 50;

    public IReadOnlyList<ConnectedPeer> GetConnectedPeers(InfoHash infoHash)
    {
        if (!_connections.TryGetValue(infoHash, out var peers))
            return [];

        return peers.Values.Select(m => new ConnectedPeer
        {
            PeerInfo = m.PeerInfo,
            PeerId = m.PeerId,
            InfoHash = infoHash,
            Bitfield = m.Client.State.PeerBitfield,
            BytesDownloaded = m.Client.BytesDownloaded,
            BytesUploaded = m.Client.BytesUploaded,
            ConnectedAt = m.ConnectedAt
        }).ToList();
    }

    public int GetConnectedPeerCount(InfoHash infoHash)
    {
        if (!_connections.TryGetValue(infoHash, out var peers))
            return 0;

        return peers.Count;
    }

    public PeerWireClient? GetPeerClient(InfoHash infoHash, PeerInfo peerInfo)
    {
        if (!_connections.TryGetValue(infoHash, out var peers))
            return null;

        return peers.TryGetValue(peerInfo, out var managed) ? managed.Client : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PeerConnectionService started with PeerId: {PeerId}", _selfId.ToAsciiString());

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectToPeers(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in PeerConnectionService loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        await DisconnectAll();
    }

    private async Task ConnectToPeers(CancellationToken stoppingToken)
    {
        var activeTorrents = torrentManager.GetTorrentsByStatus(TorrentStatus.Downloading);
        var activeSet = new HashSet<InfoHash>(activeTorrents);

        // Clean up connections for torrents that are no longer downloading
        foreach (var infoHash in _connections.Keys)
        {
            if (!activeSet.Contains(infoHash))
            {
                await DisconnectTorrent(infoHash);
            }
        }

        foreach (var infoHash in activeTorrents)
        {
            // Clean up completed/faulted connections
            CleanupDisconnectedPeers(infoHash);

            var torrentState = torrentManager.GetState(infoHash);
            if (torrentState is null)
                continue;

            var torrentPeers = _connections.GetOrAdd(infoHash, _ => new ConcurrentDictionary<PeerInfo, ManagedPeerConnection>());
            if (torrentPeers.Count >= MaxPeersPerTorrent)
                continue;

            var discoveredPeers = peerPool.GetPeers(infoHash);
            foreach (var discovered in discoveredPeers)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                if (torrentPeers.Count >= MaxPeersPerTorrent)
                    break;

                if (torrentPeers.ContainsKey(discovered.PeerInfo))
                    continue;

                var peerInfo = discovered.PeerInfo;
                var numberOfPieces = torrentState.NumberOfPieces;

                _ = Task.Run(async () =>
                {
                    await ConnectToPeer(infoHash, peerInfo, numberOfPieces, stoppingToken);
                }, stoppingToken);
            }
        }
    }

    private async Task ConnectToPeer(InfoHash infoHash, PeerInfo peerInfo, int numberOfPieces, CancellationToken stoppingToken)
    {
        if (!await _halfOpenSemaphore.WaitAsync(0, stoppingToken))
            return;

        try
        {
            // Double-check we haven't already connected
            if (_connections.TryGetValue(infoHash, out var peers) && peers.ContainsKey(peerInfo))
                return;

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                connectCts.CancelAfter(5_000);

                await socket.ConnectAsync(peerInfo.Ip, peerInfo.Port, connectCts.Token);
            }
            catch (Exception ex)
            {
                socket.Dispose();
                logger.LogDebug("Failed to connect to peer {Ip}:{Port} - {Error}", peerInfo.Ip, peerInfo.Port, ex.Message);
                return;
            }

            var connection = new PeerWireSocketConnection(socket, pwcLogger);
            try
            {
                using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                handshakeCts.CancelAfter(5_000);

                var response = await handshakeService.HandleOutbound(
                    connection.Writer,
                    connection.Reader,
                    infoHash,
                    _selfId,
                    handshakeCts.Token);

                if (!response.Success)
                {
                    logger.LogDebug("Handshake failed with peer {Ip}:{Port} - {Error}", peerInfo.Ip, peerInfo.Port, response.Error);
                    await connection.DisposeAsync();
                    return;
                }

                connection.SetInfoHash(infoHash);
                connection.SetPeerId(response.PeerId);

                var client = new PeerWireClient(connection, numberOfPieces, pwcLogger, stoppingToken);
                var processTask = Task.Run(() => client.ProcessMessages(), stoppingToken);

                var managed = new ManagedPeerConnection(client, connection, processTask, peerInfo, response.PeerId, DateTimeOffset.UtcNow);

                var torrentPeers = _connections.GetOrAdd(infoHash, _ => new ConcurrentDictionary<PeerInfo, ManagedPeerConnection>());
                if (!torrentPeers.TryAdd(peerInfo, managed))
                {
                    // Another task already connected to this peer
                    await client.DisposeAsync();
                    return;
                }

                logger.LogInformation("Connected to peer {Ip}:{Port} for torrent {InfoHash} (PeerId: {PeerId})",
                    peerInfo.Ip, peerInfo.Port, infoHash.AsString(), response.PeerId.ToAsciiString());
            }
            catch (Exception ex)
            {
                logger.LogDebug("Error during handshake with {Ip}:{Port} - {Error}", peerInfo.Ip, peerInfo.Port, ex.Message);
                await connection.DisposeAsync();
            }
        }
        finally
        {
            _halfOpenSemaphore.Release();
        }
    }

    private void CleanupDisconnectedPeers(InfoHash infoHash)
    {
        if (!_connections.TryGetValue(infoHash, out var peers))
            return;

        foreach (var (peerInfo, managed) in peers)
        {
            if (managed.ProcessTask.IsCompleted)
            {
                if (peers.TryRemove(peerInfo, out var removed))
                {
                    logger.LogInformation("Peer {Ip}:{Port} disconnected from torrent {InfoHash}",
                        peerInfo.Ip, peerInfo.Port, infoHash.AsString());

                    _ = Task.Run(async () =>
                    {
                        try { await removed.Client.DisposeAsync(); }
                        catch { /* already cleaned up */ }
                    });
                }
            }
        }
    }

    private async Task DisconnectTorrent(InfoHash infoHash)
    {
        if (!_connections.TryRemove(infoHash, out var peers))
            return;

        logger.LogInformation("Disconnecting all peers for torrent {InfoHash}", infoHash.AsString());

        foreach (var (_, managed) in peers)
        {
            try { await managed.Client.DisposeAsync(); }
            catch { /* best effort */ }
        }

        peers.Clear();
    }

    private async Task DisconnectAll()
    {
        foreach (var infoHash in _connections.Keys.ToList())
        {
            await DisconnectTorrent(infoHash);
        }
    }

    private sealed record ManagedPeerConnection(
        PeerWireClient Client,
        PeerWireSocketConnection Connection,
        Task ProcessTask,
        PeerInfo PeerInfo,
        PeerId PeerId,
        DateTimeOffset ConnectedAt);
}
