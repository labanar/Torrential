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
}
