using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Settings;

namespace Torrential.Integrations;

public sealed class IntegrationService(IServiceScopeFactory serviceScopeFactory)
{
    public async Task<List<Integration>> GetAll()
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        return await lease.Context.Integrations.AsNoTracking().ToListAsync();
    }

    public async Task<Integration?> GetById(Guid id)
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        return await lease.Context.Integrations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Integration> Create(Integration integration)
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        await lease.Context.Integrations.AddAsync(integration);
        await lease.Context.SaveChangesAsync();
        return integration;
    }

    public async Task<Integration?> Update(Guid id, Integration updated)
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        var existing = await lease.Context.Integrations.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
            return null;

        existing.Name = updated.Name;
        existing.Type = updated.Type;
        existing.Enabled = updated.Enabled;
        existing.TriggerEvent = updated.TriggerEvent;
        existing.Configuration = updated.Configuration;

        await lease.Context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> Delete(Guid id)
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        var existing = await lease.Context.Integrations.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
            return false;

        lease.Context.Integrations.Remove(existing);
        await lease.Context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Integration>> GetEnabledByTrigger(string triggerEvent)
    {
        using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
        return await lease.Context.Integrations
            .AsNoTracking()
            .Where(x => x.Enabled && x.TriggerEvent == triggerEvent)
            .ToListAsync();
    }
}
