using Microsoft.Extensions.DependencyInjection;

namespace Torrential.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTorrentApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITorrentManager, TorrentManager>();
        services.AddSingleton<IAnnounceService, AnnounceService>();
        return services;
    }
}
