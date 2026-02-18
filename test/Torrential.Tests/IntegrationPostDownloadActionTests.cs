using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Pipelines;
using Torrential.Settings;

namespace Torrential.Tests;

public sealed class IntegrationPostDownloadActionTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SettingsManager _settingsManager;
    private readonly string _tempRoot;

    public IntegrationPostDownloadActionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddDbContext<TorrentialDb>(options =>
            options.UseSqlite($"Data Source={Path.Combine(_tempRoot, "settings.db")}"));

        _serviceProvider = services.BuildServiceProvider();

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            db.Database.EnsureCreated();
        }

        _settingsManager = new SettingsManager(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IMemoryCache>());
    }

    private IntegrationPostDownloadAction CreateAction() =>
        new(_settingsManager, NullLogger<IntegrationPostDownloadAction>.Instance);

    private static InfoHash TestInfoHash => InfoHash.FromHexString("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

    [Fact]
    public async Task All_integrations_disabled_returns_success()
    {
        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Slack_enabled_with_webhook_returns_success()
    {
        await _settingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/test",
            SlackOnTorrentComplete = true,
            DiscordEnabled = false,
            DiscordWebhookUrl = "",
            DiscordOnTorrentComplete = false,
            CommandEnabled = false,
            Command = "",
            CommandOnTorrentComplete = false
        });

        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Slack_enabled_without_torrent_complete_trigger_returns_success()
    {
        await _settingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/test",
            SlackOnTorrentComplete = false,
            DiscordEnabled = false,
            DiscordWebhookUrl = "",
            DiscordOnTorrentComplete = false,
            CommandEnabled = false,
            Command = "",
            CommandOnTorrentComplete = false
        });

        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Discord_enabled_with_webhook_returns_success()
    {
        await _settingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = false,
            SlackWebhookUrl = "",
            SlackOnTorrentComplete = false,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test",
            DiscordOnTorrentComplete = true,
            CommandEnabled = false,
            Command = "",
            CommandOnTorrentComplete = false
        });

        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Command_enabled_with_value_returns_success()
    {
        await _settingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = false,
            SlackWebhookUrl = "",
            SlackOnTorrentComplete = false,
            DiscordEnabled = false,
            DiscordWebhookUrl = "",
            DiscordOnTorrentComplete = false,
            CommandEnabled = true,
            Command = "echo done",
            CommandOnTorrentComplete = true
        });

        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task All_integrations_enabled_returns_success()
    {
        await _settingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/test",
            SlackOnTorrentComplete = true,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test",
            DiscordOnTorrentComplete = true,
            CommandEnabled = true,
            Command = "echo done",
            CommandOnTorrentComplete = true
        });

        var action = CreateAction();
        var result = await action.ExecuteAsync(TestInfoHash, CancellationToken.None);
        Assert.True(result.Success);
    }

    [Fact]
    public void ContinueOnFailure_is_true()
    {
        var action = CreateAction();
        Assert.True(action.ContinueOnFailure);
    }

    [Fact]
    public void Name_is_integrations()
    {
        var action = CreateAction();
        Assert.Equal("Integrations", action.Name);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
