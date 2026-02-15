using Microsoft.EntityFrameworkCore;

namespace Torrential.Application.Persistence;

public class TorrentDbContext(DbContextOptions<TorrentDbContext> options) : DbContext(options)
{
    public DbSet<TorrentEntity> Torrents => Set<TorrentEntity>();
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TorrentEntity>(entity =>
        {
            entity.HasKey(e => e.InfoHash);
            entity.Property(e => e.InfoHash).HasMaxLength(40);
        });

        modelBuilder.Entity<SettingsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
