using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Extensions.Indexing.Models;

namespace Torrential.Extensions.Indexing.Persistence;

public interface IIndexerRepository
{
    Task<IReadOnlyList<IndexerDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<IndexerDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IndexerDefinition> AddAsync(IndexerDefinition indexer, CancellationToken ct = default);
    Task<IndexerDefinition?> UpdateAsync(IndexerDefinition indexer, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IndexerDefinition>> GetEnabledAsync(CancellationToken ct = default);
}

internal sealed class IndexerRepository(IServiceScopeFactory scopeFactory) : IIndexerRepository
{
    public async Task<IReadOnlyList<IndexerDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();
        return await db.Indexers.AsNoTracking().OrderBy(i => i.Name).ToListAsync(ct);
    }

    public async Task<IndexerDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();
        return await db.Indexers.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IndexerDefinition> AddAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();
        db.Indexers.Add(indexer);
        await db.SaveChangesAsync(ct);
        return indexer;
    }

    public async Task<IndexerDefinition?> UpdateAsync(IndexerDefinition indexer, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();

        var existing = await db.Indexers.FirstOrDefaultAsync(i => i.Id == indexer.Id, ct);
        if (existing is null) return null;

        existing.Name = indexer.Name;
        existing.Type = indexer.Type;
        existing.BaseUrl = indexer.BaseUrl;
        existing.AuthMode = indexer.AuthMode;
        existing.ApiKey = indexer.ApiKey;
        existing.Username = indexer.Username;
        existing.Password = indexer.Password;
        existing.Enabled = indexer.Enabled;

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();

        var existing = await db.Indexers.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (existing is null) return false;

        db.Indexers.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<IndexerDefinition>> GetEnabledAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexingDbContext>();
        return await db.Indexers.AsNoTracking().Where(i => i.Enabled).OrderBy(i => i.Name).ToListAsync(ct);
    }
}
