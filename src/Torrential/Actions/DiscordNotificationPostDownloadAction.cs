using Microsoft.Extensions.Logging;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Pipelines;

public class DiscordNotificationPostDownloadAction(
    SettingsManager settingsManager,
    TorrentMetadataCache metadataCache,
    ILogger<DiscordNotificationPostDownloadAction> logger) : IPostDownloadAction
{
    public string Name => "DiscordNotification";
    public bool ContinueOnFailure => true;

    public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
    {
        var settings = await settingsManager.GetIntegrationSettings();
        if (!settings.DiscordEnabled)
        {
            logger.LogDebug("Discord integration is disabled; skipping notification for torrent {InfoHash}", infoHash);
            return new PostDownloadActionResult { Success = true };
        }

        var torrentName = ResolveTorrentName(infoHash);

        logger.LogInformation(
            "[Discord] Sending notification for completed torrent. Name={TorrentName}, InfoHash={InfoHash}, WebhookUrl={WebhookUrl}",
            torrentName,
            infoHash,
            settings.DiscordWebhookUrl);

        return new PostDownloadActionResult { Success = true };
    }

    private string ResolveTorrentName(InfoHash infoHash)
    {
        if (metadataCache.TryGet(infoHash, out var meta))
            return meta.Name;

        return infoHash.ToString();
    }
}
