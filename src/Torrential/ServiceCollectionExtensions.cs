using Microsoft.Extensions.DependencyInjection;
using Torrential.Trackers;
using Torrential.Trackers.Http;
using Torrential.Trackers.Udp;

namespace Torrential
{
    public static class ServiceCollectionExtensions
    {
        public static void AddTorrential(this IServiceCollection services)
        {
            services.AddSingleton<IPeerService, PeerService>();
            services.AddHttpClient<HttpTrackerClient>();
            services.AddSingleton<ITrackerClient>(sp => sp.GetRequiredService<HttpTrackerClient>());
            services.AddSingleton<ITrackerClient, UdpTrackerClient>();
        }
    }
}
