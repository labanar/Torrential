using Torrential.Core;

namespace Torrential.Application;

public interface IInboundConnectionHandler
{
    Task<bool> IsBlockedAsync(IPeerWireConnection connection);
    Task HandleAsync(IPeerWireConnection connection);
}
