namespace Torrential.Core;

public interface IInboundConnectionHandler
{
    Task<bool> HandleAsync(IPeerWireConnection connection, CancellationToken cancellationToken);
}
