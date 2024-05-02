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
        Verifying,
        Copying,

        Error = 99,
    }

    public class TorrentialSettings
    {
        public static TorrentialSettings Current;

        public static Guid DefaultId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid Id { get; set; } = DefaultId;
        public required FileSettings FileSettings { get; set; }
    }

    public class FileSettings
    {
        public required string DownloadPath { get; set; }
        public required string CompletedPath { get; set; }
    }

}
