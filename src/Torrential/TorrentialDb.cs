using Microsoft.EntityFrameworkCore;

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
                .ComplexProperty(p => p.DefaultTorrentSettings);

            modelBuilder.Entity<TorrentialSettings>()
                .ComplexProperty(p => p.GlobalTorrentSettings);
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
        public DefaultTorrentSettings DefaultTorrentSettings { get; set; } = DefaultTorrentSettings.Default;
        public GlobalTorrentSettings GlobalTorrentSettings { get; set; } = GlobalTorrentSettings.Default;
    }

    public interface ISettingsSection<T>
        where T : ISettingsSection<T>
    {
        static abstract T Default { get; }
        static abstract string CacheKey { get; }
        static abstract bool Validate(T settings);
    }

    public class FileSettings : ISettingsSection<FileSettings>
    {
        public static string CacheKey => "settings.file";

        private static string AppPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static FileSettings Default => new()
        {
            DownloadPath = Path.Combine(AppPath, "torrential\\downloads"),
            CompletedPath = Path.Combine(AppPath, "torrential\\completed")
        };

        public static bool Validate(FileSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.DownloadPath) || string.IsNullOrWhiteSpace(settings.CompletedPath))
                return false;

            return true;
        }

        public required string DownloadPath { get; set; }
        public required string CompletedPath { get; set; }
    }

    public class TcpListenerSettings : ISettingsSection<TcpListenerSettings>
    {
        public static string CacheKey => "settings.tcpListener";
        public static TcpListenerSettings Default => new()
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

    public class DefaultTorrentSettings : ISettingsSection<DefaultTorrentSettings>
    {
        public static string CacheKey => "settings.torrent.default";
        public static DefaultTorrentSettings Default => new()
        {
            MaxConnections = 50
        };

        public static bool Validate(DefaultTorrentSettings settings)
        {
            if (settings.MaxConnections < 1)
                return false;

            return true;
        }

        public required int MaxConnections { get; set; }
    }

    public class GlobalTorrentSettings : ISettingsSection<GlobalTorrentSettings>
    {
        public static string CacheKey => "settings.torrent.global";
        public static GlobalTorrentSettings Default => new()
        {
            MaxConnections = 500
        };

        public static bool Validate(GlobalTorrentSettings settings)
        {
            if (settings.MaxConnections < 1)
                return false;

            return true;
        }

        public required int MaxConnections { get; set; }
    }

}
