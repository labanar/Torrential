using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Integrations;

public sealed class IntegrationExecutor(IntegrationService integrationService, ILogger<IntegrationExecutor> logger)
{
    public async Task HandleTorrentComplete(TorrentCompleteEvent evt) => await ExecuteIntegrations("torrent_complete", evt.InfoHash);
    public async Task HandleTorrentAdded(TorrentAddedEvent evt) => await ExecuteIntegrations("torrent_added", evt.InfoHash);
    public async Task HandleTorrentStarted(TorrentStartedEvent evt) => await ExecuteIntegrations("torrent_started", evt.InfoHash);
    public async Task HandleTorrentStopped(TorrentStoppedEvent evt) => await ExecuteIntegrations("torrent_stopped", evt.InfoHash);

    private async Task ExecuteIntegrations(string triggerEvent, InfoHash infoHash)
    {
        var integrations = await integrationService.GetEnabledByTrigger(triggerEvent);
        foreach (var integration in integrations)
        {
            logger.LogInformation("[Mock] Executing {Type} integration '{Name}' for {Event} on {InfoHash}",
                integration.Type, integration.Name, triggerEvent, infoHash);
            // Mock execution - just log. Real implementation would send webhooks or exec commands.
        }
    }
}
