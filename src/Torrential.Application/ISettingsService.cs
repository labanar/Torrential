using Torrential.Application.Persistence;

namespace Torrential.Application;

public interface ISettingsService
{
    Task<SettingsEntity> GetSettingsAsync();
    Task<SettingsEntity> UpdateSettingsAsync(string downloadFolder, string completedFolder, int maxHalfOpenConnections, int maxPeersPerTorrent);
}
