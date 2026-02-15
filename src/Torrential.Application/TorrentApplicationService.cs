using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Torrential.Application.Data;
using Torrential.Application.Events;
using Torrential.Application.Files;
using Torrential.Application.Peers;
using Torrential.Application.Settings;
using Torrential.Application.Torrents;

namespace Torrential.Application;

public record TorrentAddResult(InfoHash InfoHash, bool Success, string? Error = null);

public class TorrentApplicationService(
    IServiceScopeFactory serviceScopeFactory,
    TorrentTaskManager torrentTaskManager,
    IMetadataFileService metadataFileService,
    SettingsManager settingsManager,
    BitfieldManager bitfieldManager,
    IFileHandleProvider fileHandleProvider,
    TorrentFileService torrentFileService,
    TorrentStats torrentStats,
    IEventBus eventBus,
    ILogger<TorrentApplicationService> logger)
{
    public async Task<TorrentAddResult> AddTorrentAsync(TorrentMetadata metadata, string downloadPath, string completedPath)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();

        var fileSettings = await settingsManager.GetFileSettings();

        await db.AddAsync(new TorrentConfiguration
        {
            InfoHash = metadata.InfoHash,
            DateAdded = DateTimeOffset.UtcNow,
            CompletedPath = string.IsNullOrEmpty(completedPath) ? fileSettings.CompletedPath : completedPath,
            DownloadPath = string.IsNullOrEmpty(downloadPath) ? fileSettings.DownloadPath : downloadPath,
            Status = TorrentStatus.Idle
        });

        await metadataFileService.SaveMetadata(metadata);
        await db.SaveChangesAsync();
        await torrentTaskManager.Add(metadata);

        return new TorrentAddResult(metadata.InfoHash, true);
    }

    public async Task StartTorrentAsync(string infoHash)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();

        var torrent = await db.Torrents.FindAsync(infoHash);
        if (torrent == null)
            throw new ArgumentException("Torrent not found");

        await torrentTaskManager.Start(infoHash);

        torrent.Status = TorrentStatus.Running;
        await db.SaveChangesAsync();
    }

    public async Task StopTorrentAsync(string infoHash)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();

        await torrentTaskManager.Stop(infoHash);

        var torrent = await db.Torrents.FindAsync(infoHash);
        if (torrent == null)
            throw new ArgumentException("Torrent not found");

        torrent.Status = TorrentStatus.Stopped;
        await db.SaveChangesAsync();
    }

    public async Task RemoveTorrentAsync(string infoHash, bool deleteFiles)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();

        await db.Torrents.Where(x => x.InfoHash == infoHash).ExecuteDeleteAsync();
        await torrentTaskManager.Remove(infoHash);
        bitfieldManager.RemoveBitfields(infoHash);
        fileHandleProvider.RemovePartFileHandle(infoHash);
        await torrentStats.ClearStats(infoHash);

        if (deleteFiles)
        {
            var metadataFilePath = await torrentFileService.GetMetadataFilePath(infoHash);
            var folder = Path.GetDirectoryName(metadataFilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                logger.LogInformation("Deleting folder {Folder}", folder);
                Directory.Delete(folder, true);
            }
        }

        await eventBus.PublishAsync(new TorrentRemovedEvent { InfoHash = infoHash });
    }
}
