namespace Torrential.Tests;

public class IntegrationSettingsValidationTests
{
    [Fact]
    public void Default_settings_are_valid()
    {
        Assert.True(IntegrationSettings.Validate(IntegrationSettings.Default));
    }

    [Fact]
    public void Slack_enabled_with_webhook_is_valid()
    {
        var settings = IntegrationSettings.Default with
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/services/test"
        };
        Assert.True(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Slack_enabled_without_webhook_is_invalid()
    {
        var settings = IntegrationSettings.Default with
        {
            SlackEnabled = true,
            SlackWebhookUrl = ""
        };
        Assert.False(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Slack_enabled_with_whitespace_webhook_is_invalid()
    {
        var settings = IntegrationSettings.Default with
        {
            SlackEnabled = true,
            SlackWebhookUrl = "   "
        };
        Assert.False(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Discord_enabled_with_webhook_is_valid()
    {
        var settings = IntegrationSettings.Default with
        {
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test"
        };
        Assert.True(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Discord_enabled_without_webhook_is_invalid()
    {
        var settings = IntegrationSettings.Default with
        {
            DiscordEnabled = true,
            DiscordWebhookUrl = ""
        };
        Assert.False(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Command_enabled_with_value_is_valid()
    {
        var settings = IntegrationSettings.Default with
        {
            CommandEnabled = true,
            Command = "echo done"
        };
        Assert.True(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Command_enabled_without_value_is_invalid()
    {
        var settings = IntegrationSettings.Default with
        {
            CommandEnabled = true,
            Command = ""
        };
        Assert.False(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void Disabled_integrations_with_empty_fields_are_valid()
    {
        var settings = IntegrationSettings.Default with
        {
            SlackEnabled = false,
            SlackWebhookUrl = "",
            DiscordEnabled = false,
            DiscordWebhookUrl = "",
            CommandEnabled = false,
            Command = ""
        };
        Assert.True(IntegrationSettings.Validate(settings));
    }

    [Fact]
    public void All_integrations_enabled_with_values_is_valid()
    {
        var settings = new IntegrationSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/services/test",
            SlackOnTorrentComplete = true,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test",
            DiscordOnTorrentComplete = true,
            CommandEnabled = true,
            Command = "echo done",
            CommandOnTorrentComplete = true
        };
        Assert.True(IntegrationSettings.Validate(settings));
    }
}
