using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Torrential.Settings
{
    public sealed class SettingsManager(IServiceScopeFactory serviceScopeFactory, IMemoryCache cache)
    {
        public async ValueTask<FileSettings> GetFileSettings() => await GetSettingsSection(f => f.FileSettings);
        public async ValueTask SaveFileSettings(FileSettings settings) => await SaveSettingsSection(f => f.FileSettings, settings);

        public async ValueTask<TcpListenerSettings> GetTcpListenerSettings() => await GetSettingsSection(f => f.TcpListenerSettings);
        public async ValueTask SaveTcpListenerSettings(TcpListenerSettings settings) => await SaveSettingsSection(f => f.TcpListenerSettings, settings);

        public async ValueTask<ConnectionSettings> GetConnectionSettings() => await GetSettingsSection(f => f.ConnectionSettings);
        public async ValueTask SaveConnectionSettings(ConnectionSettings settings) => await SaveSettingsSection(f => f.ConnectionSettings, settings);

        public async ValueTask<IntegrationsSettings> GetIntegrationsSettings() => await GetSettingsSection(f => f.IntegrationsSettings);
        public async ValueTask SaveIntegrationsSettings(IntegrationsSettings settings) => await SaveSettingsSection(f => f.IntegrationsSettings, settings);


        internal async ValueTask<T> GetSettingsSection<T>(Func<TorrentialSettings, T> selector)
            where T : ISettingsSection<T>
        {
            if (cache.TryGetValue<T>(T.CacheKey, out var section) && section != null)
                return section;

            using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
            var persistedSettings = await lease.Context.Settings.AsNoTracking().FirstOrDefaultAsync();
            if (persistedSettings != null)
            {
                section = selector(persistedSettings);
                cache.Set(T.CacheKey, section);
                return section;
            }

            persistedSettings = new();
            await lease.Context.Settings.AddAsync(persistedSettings);
            await lease.Context.SaveChangesAsync();
            return selector(persistedSettings);
        }

        internal async ValueTask SaveSettingsSection<T>(Func<TorrentialSettings, T> selector, T value)
            where T : ISettingsSection<T>
        {
            if (!T.Validate(value))
                throw new ArgumentException("Invalid settings provided.");

            using var lease = new ScopedDbContextLease<TorrentialDb>(serviceScopeFactory);
            var persistedSettings = await lease.Context.Settings.FirstOrDefaultAsync();

            var section = default(T?);
            if (persistedSettings != null)
            {
                section = selector(persistedSettings);
                UpdateSection(section, value);
                cache.Set(T.CacheKey, section);
                await lease.Context.SaveChangesAsync();
                return;
            }

            persistedSettings = new();
            section = selector(persistedSettings);
            section = value;
            cache.Set(T.CacheKey, section);
            await lease.Context.Settings.AddAsync(persistedSettings);
            await lease.Context.SaveChangesAsync();
        }

        private void UpdateSection<T>(T section, T value) where T : ISettingsSection<T>
        {
            foreach (var property in typeof(T).GetProperties())
            {
                if (property.CanWrite)
                {
                    property.SetValue(section, property.GetValue(value));
                }
            }
        }
    }

    internal sealed class ScopedDbContextLease<TContext> : IDisposable
        where TContext : DbContext
    {
        private readonly IServiceScope _scope;
        private readonly TContext _db;
        public TContext Context => _db;
        public ScopedDbContextLease(IServiceScopeFactory serviceScopeFactory)
        {
            _scope = serviceScopeFactory.CreateScope();
            _db = _scope.ServiceProvider.GetRequiredService<TContext>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
