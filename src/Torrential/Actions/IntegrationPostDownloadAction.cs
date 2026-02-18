using Microsoft.Extensions.Logging;
using Torrential.Settings;

namespace Torrential.Pipelines
{
    public class IntegrationPostDownloadAction(SettingsManager settingsManager, ILogger<IntegrationPostDownloadAction> logger)
        : IPostDownloadAction
    {
        public string Name => "Integrations";
        public bool ContinueOnFailure => true;

        public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            var settings = await settingsManager.GetIntegrationSettings();

            var allSucceeded = true;

            if (settings.SlackEnabled && settings.SlackOnTorrentComplete)
            {
                allSucceeded &= ExecuteSlackHook(infoHash, settings);
            }

            if (settings.DiscordEnabled && settings.DiscordOnTorrentComplete)
            {
                allSucceeded &= ExecuteDiscordHook(infoHash, settings);
            }

            if (settings.CommandEnabled && settings.CommandOnTorrentComplete)
            {
                allSucceeded &= ExecuteCommandHook(infoHash, settings);
            }

            return new PostDownloadActionResult { Success = allSucceeded };
        }

        private bool ExecuteSlackHook(InfoHash infoHash, IntegrationSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.SlackWebhookUrl))
            {
                logger.LogWarning("Slack integration enabled but webhook URL is empty; skipping for torrent {Torrent}", infoHash);
                return false;
            }

            logger.LogInformation(
                "Mock Slack notification: POST {WebhookUrl} with payload for torrent {Torrent}",
                settings.SlackWebhookUrl,
                infoHash);
            return true;
        }

        private bool ExecuteDiscordHook(InfoHash infoHash, IntegrationSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DiscordWebhookUrl))
            {
                logger.LogWarning("Discord integration enabled but webhook URL is empty; skipping for torrent {Torrent}", infoHash);
                return false;
            }

            logger.LogInformation(
                "Mock Discord notification: POST {WebhookUrl} with payload for torrent {Torrent}",
                settings.DiscordWebhookUrl,
                infoHash);
            return true;
        }

        private bool ExecuteCommandHook(InfoHash infoHash, IntegrationSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Command))
            {
                logger.LogWarning("Command integration enabled but command is empty; skipping for torrent {Torrent}", infoHash);
                return false;
            }

            logger.LogInformation(
                "Mock command execution: '{Command}' for torrent {Torrent}",
                settings.Command,
                infoHash);
            return true;
        }
    }
}
