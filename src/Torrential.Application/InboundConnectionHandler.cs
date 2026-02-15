using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public sealed class InboundConnectionHandler : IInboundConnectionHandler
{
    private readonly HandshakeService _handshakeService;
    private readonly ITorrentManager _torrentManager;
    private readonly ILogger<InboundConnectionHandler> _logger;
    private readonly PeerId _selfId = PeerId.New;

    public InboundConnectionHandler(
        HandshakeService handshakeService,
        ITorrentManager torrentManager,
        ILogger<InboundConnectionHandler> logger)
    {
        _handshakeService = handshakeService;
        _torrentManager = torrentManager;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(IPeerWireConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeCts.CancelAfter(5_000);

            var response = await _handshakeService.HandleInbound(
                connection.Writer,
                connection.Reader,
                _selfId,
                infoHash => _torrentManager.GetState(infoHash) != null,
                handshakeCts.Token);

            if (!response.Success)
            {
                _logger.LogInformation("Inbound handshake failed - {Ip}", connection.PeerInfo.Ip);
                return false;
            }

            connection.SetInfoHash(response.InfoHash);
            connection.SetPeerId(response.PeerId);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Handshake timed out, closing connection");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inbound handshake failed");
            return false;
        }
    }
}
