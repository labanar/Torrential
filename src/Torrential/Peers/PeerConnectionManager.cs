using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Torrential.Peers;

public sealed class HalfOpenConnectionShakerService(PeerConnectionManager connectionManager, HandshakeService handshakeService, PeerSwarm swarm, ILogger<HalfOpenConnectionShakerService> logger)
    : BackgroundService
{
    private Channel<Task> _connectionTasks = Channel.CreateBounded<Task>(new BoundedChannelOptions(50)
    {
        SingleWriter = true,
        SingleReader = true
    });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processConnections = ProcessHalfOpenConnections(stoppingToken);
        var processConnectionTasks = ProcessConnectionTasks(stoppingToken);
        await Task.WhenAll(processConnections, processConnectionTasks);
    }

    private async Task ProcessConnectionTasks(CancellationToken stoppingToken)
    {
        await foreach (var task in _connectionTasks.Reader.ReadAllAsync(stoppingToken))
        {
            await task;
        }
    }

    private async Task ProcessHalfOpenConnections(CancellationToken stoppingToken)
    {
        await foreach (var halfOpenConnection in connectionManager.HalfOpenConnections.Reader.ReadAllAsync(stoppingToken))
        {
            logger.LogInformation("Processing half-open connection");
            if (halfOpenConnection.IsOutbound)
            {
                await _connectionTasks.Writer.WriteAsync(ConnectOutbound(halfOpenConnection));
            }
            else
            {
                await _connectionTasks.Writer.WriteAsync(ConnectInbound(halfOpenConnection));
            }
        }
    }

    private async Task ConnectOutbound(HalfOpenConnection halfOpenConnection)
    {
        var connection = halfOpenConnection.Connection;

        try
        {
            //Shoud we just handshake here and call it a day?
            var handshakeCts = new CancellationTokenSource(5_000);
            var response = await handshakeService.HandleOutbound(connection.Writer, connection.Reader, halfOpenConnection.InfoHash!.Value, handshakeCts.Token);

            if (!response.Success)
            {
                await connection.DisposeAsync();
                return;
            }

            connection.SetInfoHash(response.InfoHash);
            connection.SetPeerId(response.PeerId);
            await swarm.AddToSwarm(connection);
        }
        catch (OperationCanceledException oce)
        {
            logger.LogError(oce, "Handshake timed out, closing connection");
            await connection.DisposeAsync();
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbound handshake failed");
            await connection.DisposeAsync();
            return;
        }
    }

    private async Task ConnectInbound(HalfOpenConnection halfOpenConnection)
    {
        var connection = halfOpenConnection.Connection;
        try
        {
            //Shoud we just handshake here and call it a day?
            var handshakeCts = new CancellationTokenSource(5_000);
            var response = await handshakeService.HandleInbound(connection.Writer, connection.Reader, handshakeCts.Token);

            if (!response.Success)
            {
                await connection.DisposeAsync();
                return;
            }

            connection.SetInfoHash(response.InfoHash);
            connection.SetPeerId(response.PeerId);
            await swarm.AddToSwarm(connection);
        }
        catch (OperationCanceledException oce)
        {
            logger.LogError(oce, "Handshake timed out, closing connection");
            await connection.DisposeAsync();
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inbound handshake failed");
            await connection.DisposeAsync();
            return;
        }
    }
}

public sealed class PeerConnectionManager(ILogger<PeerConnectionManager> logger)
{
    public readonly Channel<HalfOpenConnection> HalfOpenConnections = Channel.CreateBounded<HalfOpenConnection>(new BoundedChannelOptions(50)
    {
        SingleReader = true,
        SingleWriter = false
    });

    public async Task QueueInboundConnection(IPeerWireConnection connection)
    {
        var halfOpenConnection = HalfOpenConnection.Inbound(connection);
        if (!HalfOpenConnections.Writer.TryWrite(halfOpenConnection))
        {
            logger.LogInformation("Max half open connections reached, closing connection");
            await connection.DisposeAsync();
            return;
        }

        logger.LogInformation("Inbound connection queued successfully");
    }

    public async Task QueueOutboundConnection(IPeerWireConnection connection, InfoHash expectedHash)
    {
        var halfOpenConnection = HalfOpenConnection.Outbound(connection, expectedHash);
        if (!HalfOpenConnections.Writer.TryWrite(halfOpenConnection))
        {
            logger.LogInformation("Max half open connections reached, closing connection");
            await connection.DisposeAsync();
            return;
        }
        logger.LogInformation("Outbound connection queued successfully");
    }
}



public class HalfOpenConnection : IAsyncDisposable
{
    public IPeerWireConnection Connection { get; }
    public bool IsOutbound { get; }
    public InfoHash? InfoHash { get; }

    private HalfOpenConnection(IPeerWireConnection connection, bool isOutbound, InfoHash? expectedHash)
    {
        Connection = connection;
        IsOutbound = isOutbound;
        InfoHash = expectedHash;
    }

    public static HalfOpenConnection Inbound(IPeerWireConnection connection) =>
         new HalfOpenConnection(connection, false, null);

    public static HalfOpenConnection Outbound(IPeerWireConnection connection, InfoHash infoHash) =>
         new HalfOpenConnection(connection, true, infoHash);

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
