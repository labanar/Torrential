using Microsoft.Extensions.DependencyInjection;
using Torrential.Core;

namespace Torrential.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTorrentApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITorrentManager, TorrentManager>();
        services.AddSingleton<IInboundConnectionHandler, InboundConnectionHandler>();
        return services;
    }
}
