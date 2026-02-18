namespace Torrential.Web.Api.Requests.Settings
{
    public class IntegrationSettingsUpdateRequest
    {
        public required bool SlackEnabled { get; set; }
        public required string SlackWebhookUrl { get; set; }
        public required bool SlackOnTorrentComplete { get; set; }

        public required bool DiscordEnabled { get; set; }
        public required string DiscordWebhookUrl { get; set; }
        public required bool DiscordOnTorrentComplete { get; set; }

        public required bool CommandEnabled { get; set; }
        public required string Command { get; set; }
        public required bool CommandOnTorrentComplete { get; set; }
    }
}
