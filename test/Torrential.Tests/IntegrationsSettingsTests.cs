using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Settings;

namespace Torrential.Tests;

public sealed class IntegrationsSettingsTests
{
    [Fact]
    public async Task Get_integrations_settings_returns_defaults_when_not_persisted()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-integrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? serviceProvider = null;

        try
        {
            (serviceProvider, var settingsManager) = await CreateInfrastructure(tempRoot);

            var settings = await settingsManager.GetIntegrationsSettings();
            var defaults = IntegrationsSettings.Default;

            Assert.Equal(defaults.SlackEnabled, settings.SlackEnabled);
            Assert.Equal(defaults.SlackWebhookUrl, settings.SlackWebhookUrl);
            Assert.Equal(defaults.SlackMessageTemplate, settings.SlackMessageTemplate);
            Assert.Equal(defaults.SlackTriggerDownloadComplete, settings.SlackTriggerDownloadComplete);
            Assert.Equal(defaults.DiscordEnabled, settings.DiscordEnabled);
            Assert.Equal(defaults.DiscordWebhookUrl, settings.DiscordWebhookUrl);
            Assert.Equal(defaults.DiscordMessageTemplate, settings.DiscordMessageTemplate);
            Assert.Equal(defaults.DiscordTriggerDownloadComplete, settings.DiscordTriggerDownloadComplete);
            Assert.Equal(defaults.CommandHookEnabled, settings.CommandHookEnabled);
            Assert.Equal(defaults.CommandTemplate, settings.CommandTemplate);
            Assert.Equal(defaults.CommandWorkingDirectory, settings.CommandWorkingDirectory);
            Assert.Equal(defaults.CommandTriggerDownloadComplete, settings.CommandTriggerDownloadComplete);
        }
        finally
        {
            serviceProvider?.Dispose();
            TryCleanup(tempRoot);
        }
    }

    [Fact]
    public async Task Save_integrations_settings_persists_values_across_manager_instances()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-integrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? firstProvider = null;
        ServiceProvider? secondProvider = null;

        var expected = new IntegrationsSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/services/T000/B000/abc",
            SlackMessageTemplate = "Done: {name}",
            SlackTriggerDownloadComplete = true,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            DiscordMessageTemplate = "Completed {name}",
            DiscordTriggerDownloadComplete = false,
            CommandHookEnabled = true,
            CommandTemplate = "refresh-jellyfin --item \"{name}\"",
            CommandWorkingDirectory = "/tmp",
            CommandTriggerDownloadComplete = true
        };

        try
        {
            (firstProvider, var firstManager) = await CreateInfrastructure(tempRoot);
            await firstManager.SaveIntegrationsSettings(expected);

            firstProvider.Dispose();
            firstProvider = null;

            (secondProvider, var secondManager) = await CreateInfrastructure(tempRoot);
            var reloaded = await secondManager.GetIntegrationsSettings();

            Assert.Equal(expected.SlackEnabled, reloaded.SlackEnabled);
            Assert.Equal(expected.SlackWebhookUrl, reloaded.SlackWebhookUrl);
            Assert.Equal(expected.SlackMessageTemplate, reloaded.SlackMessageTemplate);
            Assert.Equal(expected.SlackTriggerDownloadComplete, reloaded.SlackTriggerDownloadComplete);
            Assert.Equal(expected.DiscordEnabled, reloaded.DiscordEnabled);
            Assert.Equal(expected.DiscordWebhookUrl, reloaded.DiscordWebhookUrl);
            Assert.Equal(expected.DiscordMessageTemplate, reloaded.DiscordMessageTemplate);
            Assert.Equal(expected.DiscordTriggerDownloadComplete, reloaded.DiscordTriggerDownloadComplete);
            Assert.Equal(expected.CommandHookEnabled, reloaded.CommandHookEnabled);
            Assert.Equal(expected.CommandTemplate, reloaded.CommandTemplate);
            Assert.Equal(expected.CommandWorkingDirectory, reloaded.CommandWorkingDirectory);
            Assert.Equal(expected.CommandTriggerDownloadComplete, reloaded.CommandTriggerDownloadComplete);
        }
        finally
        {
            firstProvider?.Dispose();
            secondProvider?.Dispose();
            TryCleanup(tempRoot);
        }
    }

    [Fact]
    public async Task Save_integrations_settings_rejects_invalid_payload()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-integrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? serviceProvider = null;

        try
        {
            (serviceProvider, var settingsManager) = await CreateInfrastructure(tempRoot);
            var invalidSettings = new IntegrationsSettings
            {
                SlackEnabled = true,
                SlackWebhookUrl = "not-a-webhook-url",
                SlackMessageTemplate = "Done {name}",
                SlackTriggerDownloadComplete = true,
                DiscordEnabled = false,
                DiscordWebhookUrl = "",
                DiscordMessageTemplate = "Done {name}",
                DiscordTriggerDownloadComplete = true,
                CommandHookEnabled = false,
                CommandTemplate = "",
                CommandWorkingDirectory = "",
                CommandTriggerDownloadComplete = true
            };

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await settingsManager.SaveIntegrationsSettings(invalidSettings));
        }
        finally
        {
            serviceProvider?.Dispose();
            TryCleanup(tempRoot);
        }
    }

    private static async Task<(ServiceProvider serviceProvider, SettingsManager settingsManager)> CreateInfrastructure(string tempRoot)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDbContext<TorrentialDb>(options => options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));

        var serviceProvider = services.BuildServiceProvider();
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            await db.Database.EnsureCreatedAsync();
        }

        var settingsManager = new SettingsManager(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IMemoryCache>());

        return (serviceProvider, settingsManager);
    }

    private static void TryCleanup(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup on Windows when file handles are slow to release.
        }
    }
}
