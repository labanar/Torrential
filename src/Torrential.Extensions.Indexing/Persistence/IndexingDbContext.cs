using Microsoft.EntityFrameworkCore;
using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Persistence;

public class IndexingDbContext : DbContext
{
    public DbSet<IndexerDefinition> Indexers { get; set; }

    public IndexingDbContext(DbContextOptions<IndexingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IndexerDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Type)
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<IndexerType>(v));

            entity.Property(e => e.AuthMode)
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<AuthMode>(v));

            entity.Property(e => e.ApiKey)
                .HasMaxLength(500);

            entity.Property(e => e.Username)
                .HasMaxLength(200);

            entity.Property(e => e.Password)
                .HasMaxLength(200);
        });
    }
}
