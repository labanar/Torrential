using Torrential.Core;

namespace Torrential.Application;

public interface IInboundConnectionHandler
{
    Task<bool> HandleAsync(IPeerWireConnection connection, CancellationToken cancellationToken);
}
