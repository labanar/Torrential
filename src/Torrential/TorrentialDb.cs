using Microsoft.EntityFrameworkCore;
using Torrential.Files;

namespace Torrential
{
    public class TorrentialDb : DbContext
    {
        public DbSet<TorrentConfiguration> Torrents { get; set; }
        public DbSet<TorrentialSettings> Settings { get; set; }
        public TorrentialDb(DbContextOptions<TorrentialDb> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TorrentConfiguration>()
                .HasKey(x => x.InfoHash);

            modelBuilder.Entity<TorrentConfiguration>()
                .Property(p => p.Status)
                .HasConversion(
                    v => v.ToString(),
                    v => (TorrentStatus)Enum.Parse(typeof(TorrentStatus), v));

            modelBuilder.Entity<TorrentialSettings>()
                .ComplexProperty(p => p.FileSettings);

            modelBuilder.Entity<TorrentialSettings>()
                .ComplexProperty(p => p.TcpListenerSettings);

            modelBuilder.Entity<TorrentialSettings>()
                .ComplexProperty(p => p.ConnectionSettings);

            modelBuilder.Entity<TorrentialSettings>()
                .ComplexProperty(p => p.IntegrationsSettings);
        }
    }

    public class TorrentConfiguration
    {
        public string InfoHash { get; set; }
        public string DownloadPath { get; set; }
        public string CompletedPath { get; set; }
        public TorrentStatus Status { get; set; }
        public DateTimeOffset DateAdded { get; set; }
        public DateTimeOffset? DateCompleted { get; set; }
    }

    public class TorrentPieces
    {
        public string InfoHash { get; set; }
        public int PieceIndex { get; set; }
        public bool Downloaded { get; set; }
        public bool Verified { get; set; }
    }

    public enum TorrentStatus
    {
        Idle,
        Verifying,
        Running,
        Stopped,
        Copying,

        Error = 99,
    }

    public class TorrentialSettings
    {
        public static Guid DefaultId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid Id { get; set; } = DefaultId;
        public FileSettings FileSettings { get; set; } = FileSettings.Default;
        public TcpListenerSettings TcpListenerSettings { get; set; } = TcpListenerSettings.Default;
        public ConnectionSettings ConnectionSettings { get; set; } = ConnectionSettings.Default;
        public IntegrationsSettings IntegrationsSettings { get; set; } = IntegrationsSettings.Default;
    }

    public interface ISettingsSection<T>
        where T : ISettingsSection<T>
    {
        static abstract T Default { get; }
        static abstract string CacheKey { get; }
        static abstract bool Validate(T settings);
    }

    public record FileSettings : ISettingsSection<FileSettings>
    {
        public static string CacheKey => "settings.file";

        public static FileSettings Default => MakeDefault();

        private static FileSettings MakeDefault()
        {
            var downloadPath = Environment.GetEnvironmentVariable("DOWNLOAD_PATH") ?? Path.Combine(FileUtilities.AppDataPath, "download");
            var completedPath = Environment.GetEnvironmentVariable("COMPLETED_PATH") ?? Path.Combine(FileUtilities.AppDataPath, "completed");

            return new FileSettings
            {
                DownloadPath = downloadPath,
                CompletedPath = completedPath
            };
        }

        public static bool Validate(FileSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DownloadPath) || string.IsNullOrWhiteSpace(settings.CompletedPath))
                return false;

            return true;
        }

        public required string DownloadPath { get; set; }
        public required string CompletedPath { get; set; }
    }

    public record TcpListenerSettings : ISettingsSection<TcpListenerSettings>
    {
        public static string CacheKey => "settings.tcpListener";
        public static TcpListenerSettings Default { get; } = new()
        {
            Enabled = true,
            Port = 53123
        };

        public static bool Validate(TcpListenerSettings settings)
        {
            if (settings.Port < 0 || settings.Port > 65535)
                return false;

            return true;
        }

        public bool Enabled { get; set; }
        public required int Port { get; set; }
    }


    public record ConnectionSettings : ISettingsSection<ConnectionSettings>
    {
        public static ConnectionSettings Default { get; } = new()
        {
            MaxConnectionsPerTorrent = 50,
            MaxConnectionsGlobal = 200,
            MaxHalfOpenConnections = 50
        };

        public static string CacheKey => "settings.connections";

        public required int MaxConnectionsPerTorrent { get; set; }
        public required int MaxConnectionsGlobal { get; set; }
        public required int MaxHalfOpenConnections { get; set; }

        public static bool Validate(ConnectionSettings settings)
        {
            if (settings.MaxConnectionsGlobal < 1)
                return false;

            if (settings.MaxConnectionsPerTorrent < 1)
                return false;

            if (settings.MaxHalfOpenConnections < 1)
                return false;

            return true;
        }
    }

    public record IntegrationsSettings : ISettingsSection<IntegrationsSettings>
    {
        public static IntegrationsSettings Default { get; } = new()
        {
            SlackEnabled = false,
            SlackWebhookUrl = "",
            SlackMessageTemplate = "Download completed: {name}",
            SlackTriggerDownloadComplete = true,
            DiscordEnabled = false,
            DiscordWebhookUrl = "",
            DiscordMessageTemplate = "Download completed: {name}",
            DiscordTriggerDownloadComplete = true,
            CommandHookEnabled = false,
            CommandTemplate = "",
            CommandWorkingDirectory = "",
            CommandTriggerDownloadComplete = true
        };

        public static string CacheKey => "settings.integrations";

        public required bool SlackEnabled { get; set; }
        public required string SlackWebhookUrl { get; set; }
        public required string SlackMessageTemplate { get; set; }
        public required bool SlackTriggerDownloadComplete { get; set; }

        public required bool DiscordEnabled { get; set; }
        public required string DiscordWebhookUrl { get; set; }
        public required string DiscordMessageTemplate { get; set; }
        public required bool DiscordTriggerDownloadComplete { get; set; }

        public required bool CommandHookEnabled { get; set; }
        public required string CommandTemplate { get; set; }
        public string? CommandWorkingDirectory { get; set; }
        public required bool CommandTriggerDownloadComplete { get; set; }

        public static bool Validate(IntegrationsSettings settings)
        {
            if (settings.SlackEnabled && !TryValidateWebhookUrl(settings.SlackWebhookUrl))
                return false;

            if (settings.DiscordEnabled && !TryValidateWebhookUrl(settings.DiscordWebhookUrl))
                return false;

            if (settings.SlackEnabled && string.IsNullOrWhiteSpace(settings.SlackMessageTemplate))
                return false;

            if (settings.DiscordEnabled && string.IsNullOrWhiteSpace(settings.DiscordMessageTemplate))
                return false;

            if (settings.CommandHookEnabled && string.IsNullOrWhiteSpace(settings.CommandTemplate))
                return false;

            return true;
        }

        private static bool TryValidateWebhookUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
