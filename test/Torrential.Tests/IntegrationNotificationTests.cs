using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential;
using Torrential.Pipelines;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Tests;

public sealed class IntegrationNotificationTests
{
    [Fact]
    public async Task Integration_settings_default_to_disabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        var settings = await harness.SettingsManager.GetIntegrationSettings();

        Assert.False(settings.SlackEnabled);
        Assert.Equal("", settings.SlackWebhookUrl);
        Assert.False(settings.DiscordEnabled);
        Assert.Equal("", settings.DiscordWebhookUrl);
    }

    [Fact]
    public async Task Saved_integration_settings_round_trip_through_database()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();

        // Ensure the settings row exists (first read seeds defaults)
        _ = await harness.SettingsManager.GetIntegrationSettings();

        await harness.SettingsManager.SaveIntegrationSettings(new IntegrationSettings
        {
            SlackEnabled = true,
            SlackWebhookUrl = "https://hooks.slack.com/test",
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/test"
        });

        // Clear cache to force database read
        harness.ClearCache();

        var settings = await harness.SettingsManager.GetIntegrationSettings();
        Assert.True(settings.SlackEnabled);
        Assert.Equal("https://hooks.slack.com/test", settings.SlackWebhookUrl);
        Assert.True(settings.DiscordEnabled);
        Assert.Equal("https://discord.com/api/webhooks/test", settings.DiscordWebhookUrl);
    }

    [Fact]
    public async Task Slack_action_skips_when_disabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        // Default settings have Slack disabled
        var result = await harness.SlackAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(harness.SlackLogger.LogEntries, e =>
            e.LogLevel == LogLevel.Information && e.Message.Contains("[Slack]"));
    }

    [Fact]
    public async Task Discord_action_skips_when_disabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        // Default settings have Discord disabled
        var result = await harness.DiscordAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(harness.DiscordLogger.LogEntries, e =>
            e.LogLevel == LogLevel.Information && e.Message.Contains("[Discord]"));
    }

    [Fact]
    public async Task Slack_action_logs_notification_when_enabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        await harness.EnableSlack("https://hooks.slack.com/services/test");

        var result = await harness.SlackAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(harness.SlackLogger.LogEntries, e =>
            e.LogLevel == LogLevel.Information && e.Message.Contains("[Slack]"));
    }

    [Fact]
    public async Task Discord_action_logs_notification_when_enabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        await harness.EnableDiscord("https://discord.com/api/webhooks/test");

        var result = await harness.DiscordAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(harness.DiscordLogger.LogEntries, e =>
            e.LogLevel == LogLevel.Information && e.Message.Contains("[Discord]"));
    }

    [Fact]
    public async Task Slack_action_resolves_torrent_name_from_metadata_cache()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        await harness.EnableSlack("https://hooks.slack.com/services/test");

        var result = await harness.SlackAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(harness.SlackLogger.LogEntries, e =>
            e.Message.Contains("test-torrent"));
    }

    [Fact]
    public async Task Discord_action_resolves_torrent_name_from_metadata_cache()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        await harness.EnableDiscord("https://discord.com/api/webhooks/test");

        var result = await harness.DiscordAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(harness.DiscordLogger.LogEntries, e =>
            e.Message.Contains("test-torrent"));
    }

    [Fact]
    public async Task Both_actions_succeed_when_both_enabled()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();
        await harness.EnableSlack("https://hooks.slack.com/services/test");
        await harness.EnableDiscord("https://discord.com/api/webhooks/test");

        var slackResult = await harness.SlackAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);
        var discordResult = await harness.DiscordAction.ExecuteAsync(harness.TestInfoHash, CancellationToken.None);

        Assert.True(slackResult.Success);
        Assert.True(discordResult.Success);
    }

    [Fact]
    public async Task Executor_continues_past_notification_actions_on_failure()
    {
        // Both Slack and Discord actions have ContinueOnFailure = true,
        // so the executor should continue even if preceding actions fail.
        await using var harness = await IntegrationTestHarness.CreateAsync();

        var failingAction = new AlwaysFailsAction(continueOnFailure: true);
        var executor = new PostDownloadActionExecutor(
            [failingAction, harness.SlackAction],
            NullLogger<PostDownloadActionExecutor>.Instance);

        await harness.EnableSlack("https://hooks.slack.com/services/test");

        var evt = new TorrentCompleteEvent { InfoHash = harness.TestInfoHash };
        await executor.HandleTorrentComplete(evt);

        // Slack action should still have executed despite the preceding failure
        Assert.Contains(harness.SlackLogger.LogEntries, e =>
            e.LogLevel == LogLevel.Information && e.Message.Contains("[Slack]"));
    }

    [Fact]
    public async Task Executor_halts_when_non_continuable_action_fails()
    {
        await using var harness = await IntegrationTestHarness.CreateAsync();

        var failingAction = new AlwaysFailsAction(continueOnFailure: false);
        var executor = new PostDownloadActionExecutor(
            [failingAction, harness.SlackAction],
            NullLogger<PostDownloadActionExecutor>.Instance);

        await harness.EnableSlack("https://hooks.slack.com/services/test");

        var evt = new TorrentCompleteEvent { InfoHash = harness.TestInfoHash };
        await Assert.ThrowsAsync<Exception>(() => executor.HandleTorrentComplete(evt));

        // Slack action should NOT have executed because the failing action halted execution
        Assert.Empty(harness.SlackLogger.LogEntries);
    }

    [Fact]
    public void Slack_action_has_continue_on_failure_enabled()
    {
        using var harness = IntegrationTestHarness.CreateMinimal();
        Assert.True(harness.SlackAction.ContinueOnFailure);
    }

    [Fact]
    public void Discord_action_has_continue_on_failure_enabled()
    {
        using var harness = IntegrationTestHarness.CreateMinimal();
        Assert.True(harness.DiscordAction.ContinueOnFailure);
    }

    private sealed class AlwaysFailsAction(bool continueOnFailure) : IPostDownloadAction
    {
        public string Name => "AlwaysFails";
        public bool ContinueOnFailure => continueOnFailure;

        public Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PostDownloadActionResult { Success = false });
        }
    }

    internal sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    internal record LogEntry(LogLevel LogLevel, string Message);

    private sealed class IntegrationTestHarness : IAsyncDisposable, IDisposable
    {
        private readonly ServiceProvider? _serviceProvider;
        private readonly string? _tempRoot;
        private readonly IMemoryCache? _cache;

        private IntegrationTestHarness(
            ServiceProvider? serviceProvider,
            string? tempRoot,
            IMemoryCache? cache,
            SettingsManager settingsManager,
            TorrentMetadataCache metadataCache,
            SlackNotificationPostDownloadAction slackAction,
            DiscordNotificationPostDownloadAction discordAction,
            CapturingLogger<SlackNotificationPostDownloadAction> slackLogger,
            CapturingLogger<DiscordNotificationPostDownloadAction> discordLogger,
            InfoHash testInfoHash)
        {
            _serviceProvider = serviceProvider;
            _tempRoot = tempRoot;
            _cache = cache;
            SettingsManager = settingsManager;
            MetadataCache = metadataCache;
            SlackAction = slackAction;
            DiscordAction = discordAction;
            SlackLogger = slackLogger;
            DiscordLogger = discordLogger;
            TestInfoHash = testInfoHash;
        }

        public SettingsManager SettingsManager { get; }
        public TorrentMetadataCache MetadataCache { get; }
        public SlackNotificationPostDownloadAction SlackAction { get; }
        public DiscordNotificationPostDownloadAction DiscordAction { get; }
        public CapturingLogger<SlackNotificationPostDownloadAction> SlackLogger { get; }
        public CapturingLogger<DiscordNotificationPostDownloadAction> DiscordLogger { get; }
        public InfoHash TestInfoHash { get; }

        public static async Task<IntegrationTestHarness> CreateAsync()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-integration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging();
            services.AddDbContext<TorrentialDb>(options =>
                options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));

            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
                await db.Database.EnsureCreatedAsync();
            }

            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            var settingsManager = new SettingsManager(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                cache);

            var metadataCache = new TorrentMetadataCache();
            InfoHash testInfoHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            metadataCache.Add(new TorrentMetadata
            {
                Name = "test-torrent",
                InfoHash = testInfoHash,
                UrlList = Array.Empty<string>(),
                AnnounceList = Array.Empty<string>(),
                Files = [],
                PieceSize = 16 * 1024,
                TotalSize = 0,
                PieceHashesConcatenated = new byte[20]
            });

            var slackLogger = new CapturingLogger<SlackNotificationPostDownloadAction>();
            var discordLogger = new CapturingLogger<DiscordNotificationPostDownloadAction>();

            var slackAction = new SlackNotificationPostDownloadAction(settingsManager, metadataCache, slackLogger);
            var discordAction = new DiscordNotificationPostDownloadAction(settingsManager, metadataCache, discordLogger);

            return new IntegrationTestHarness(
                serviceProvider, tempRoot, cache,
                settingsManager, metadataCache,
                slackAction, discordAction,
                slackLogger, discordLogger,
                testInfoHash);
        }

        /// <summary>
        /// Lightweight harness for synchronous property assertions (no DB needed).
        /// </summary>
        public static IntegrationTestHarness CreateMinimal()
        {
            var metadataCache = new TorrentMetadataCache();
            var slackLogger = new CapturingLogger<SlackNotificationPostDownloadAction>();
            var discordLogger = new CapturingLogger<DiscordNotificationPostDownloadAction>();

            var slackAction = new SlackNotificationPostDownloadAction(null!, metadataCache, slackLogger);
            var discordAction = new DiscordNotificationPostDownloadAction(null!, metadataCache, discordLogger);

            return new IntegrationTestHarness(
                null, null, null,
                null!, metadataCache,
                slackAction, discordAction,
                slackLogger, discordLogger,
                (InfoHash)"0000000000000000000000000000000000000000");
        }

        public async Task EnableSlack(string webhookUrl)
        {
            var current = await SettingsManager.GetIntegrationSettings();
            await SettingsManager.SaveIntegrationSettings(new IntegrationSettings
            {
                SlackEnabled = true,
                SlackWebhookUrl = webhookUrl,
                DiscordEnabled = current.DiscordEnabled,
                DiscordWebhookUrl = current.DiscordWebhookUrl
            });
        }

        public async Task EnableDiscord(string webhookUrl)
        {
            var current = await SettingsManager.GetIntegrationSettings();
            await SettingsManager.SaveIntegrationSettings(new IntegrationSettings
            {
                SlackEnabled = current.SlackEnabled,
                SlackWebhookUrl = current.SlackWebhookUrl,
                DiscordEnabled = true,
                DiscordWebhookUrl = webhookUrl
            });
        }

        public void ClearCache()
        {
            _cache?.Remove(IntegrationSettings.CacheKey);
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            TryCleanup();
        }

        public async ValueTask DisposeAsync()
        {
            _serviceProvider?.Dispose();
            TryCleanup();
            await ValueTask.CompletedTask;
        }

        private void TryCleanup()
        {
            if (_tempRoot == null || !Directory.Exists(_tempRoot))
                return;

            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup on Windows when file handles are slow to release.
            }
        }
    }
}
