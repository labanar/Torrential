namespace Torrential.Web.Api.Requests.Settings
{
    public class IntegrationSettingsUpdateRequest
    {
        public required bool SlackEnabled { get; init; }
        public required string SlackWebhookUrl { get; init; }
        public required bool DiscordEnabled { get; init; }
        public required string DiscordWebhookUrl { get; init; }
    }
}
