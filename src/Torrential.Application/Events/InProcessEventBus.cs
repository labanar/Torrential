using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Torrential.Application.Events;

public sealed class InProcessEventBus(IServiceScopeFactory serviceScopeFactory, ILogger<InProcessEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling event {EventType} in handler {HandlerType}",
                    typeof(TEvent).Name, handler.GetType().Name);
            }
        }
    }
}
