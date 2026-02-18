namespace Torrential.Web.Api.Requests.Settings
{
    public class IntegrationsSettingsUpdateRequest
    {
        public required bool SlackEnabled { get; init; }
        public required string SlackWebhookUrl { get; init; }
        public required string SlackMessageTemplate { get; init; }
        public required bool SlackTriggerDownloadComplete { get; init; }

        public required bool DiscordEnabled { get; init; }
        public required string DiscordWebhookUrl { get; init; }
        public required string DiscordMessageTemplate { get; init; }
        public required bool DiscordTriggerDownloadComplete { get; init; }

        public required bool CommandHookEnabled { get; init; }
        public required string CommandTemplate { get; init; }
        public string? CommandWorkingDirectory { get; init; }
        public required bool CommandTriggerDownloadComplete { get; init; }
    }
}
