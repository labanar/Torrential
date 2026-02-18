using Microsoft.Extensions.Logging;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Pipelines;

public class SlackNotificationPostDownloadAction(
    SettingsManager settingsManager,
    TorrentMetadataCache metadataCache,
    ILogger<SlackNotificationPostDownloadAction> logger) : IPostDownloadAction
{
    public string Name => "SlackNotification";
    public bool ContinueOnFailure => true;

    public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
    {
        var settings = await settingsManager.GetIntegrationSettings();
        if (!settings.SlackEnabled)
        {
            logger.LogDebug("Slack integration is disabled; skipping notification for torrent {InfoHash}", infoHash);
            return new PostDownloadActionResult { Success = true };
        }

        var torrentName = ResolveTorrentName(infoHash);

        logger.LogInformation(
            "[Slack] Sending notification for completed torrent. Name={TorrentName}, InfoHash={InfoHash}, WebhookUrl={WebhookUrl}",
            torrentName,
            infoHash,
            settings.SlackWebhookUrl);

        return new PostDownloadActionResult { Success = true };
    }

    private string ResolveTorrentName(InfoHash infoHash)
    {
        if (metadataCache.TryGet(infoHash, out var meta))
            return meta.Name;

        return infoHash.ToString();
    }
}
