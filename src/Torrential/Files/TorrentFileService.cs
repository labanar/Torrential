using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Files;

public sealed class TorrentFileService(TorrentMetadataCache metaCache, SettingsManager settingsManager, IServiceScopeFactory? scopeFactory = null)
{
    private ConcurrentDictionary<InfoHash, string> _downloadPaths = [];
    private ConcurrentDictionary<InfoHash, string> _completedPaths = [];
    private ConcurrentDictionary<InfoHash, string> _partPaths = [];
    private ConcurrentDictionary<InfoHash, string> _metadataPaths = [];
    private ConcurrentDictionary<InfoHash, string> _downloadBitFieldPath = [];
    private ConcurrentDictionary<InfoHash, string> _verificationBitFieldPath = [];

    public Task ClearData(InfoHash infoHash)
    {
        _downloadBitFieldPath.TryRemove(infoHash, out _);
        _completedPaths.TryRemove(infoHash, out _);
        _downloadPaths.TryRemove(infoHash, out _);
        _metadataPaths.TryRemove(infoHash, out _);
        _partPaths.TryRemove(infoHash, out _);
        _verificationBitFieldPath.TryRemove(infoHash, out _);
        return Task.CompletedTask;
    }

    public async Task<string> GetMetadataFilePath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            await BuildPathCache(infoHash);

        return _metadataPaths[infoHash];
    }

    public async Task<string> GetDownloadBitFieldPath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            await BuildPathCache(infoHash);

        return _downloadBitFieldPath[infoHash];
    }

    public async Task<string> GetVerificationBitFieldPath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            await BuildPathCache(infoHash);

        return _verificationBitFieldPath[infoHash];
    }

    public async Task<string> GetPartFilePath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            await BuildPathCache(infoHash);

        return _partPaths[infoHash];
    }

    public async Task<string> GetCompletedTorrentPath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            await BuildPathCache(infoHash);

        return _completedPaths[infoHash];
    }

    private async Task<string> BuildPathCache(InfoHash infoHash)
    {
        if (_downloadPaths.TryGetValue(infoHash, out var path))
            return path;

        if (!metaCache.TryGet(infoHash, out var metaData))
            throw new InvalidOperationException("Torrent metadata not found");

        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(metaData.Name));
        var (downloadRoot, completedRoot) = await ResolveRootPaths(infoHash);

        path = Path.Combine(downloadRoot, torrentName);
        _downloadPaths.TryAdd(infoHash, path);
        _completedPaths.TryAdd(infoHash, Path.Combine(completedRoot, torrentName));
        var settings = await settingsManager.GetFileSettings();

        var downloadBase = settings.DownloadPath;
        var completedBase = settings.CompletedPath;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            var config = await db.Torrents.AsNoTracking()
                .FirstOrDefaultAsync(t => t.InfoHash == infoHash.AsString());

            if (config != null)
            {
                if (!string.IsNullOrWhiteSpace(config.DownloadPath))
                    downloadBase = config.DownloadPath;

                if (!string.IsNullOrWhiteSpace(config.CompletedPath))
                    completedBase = config.CompletedPath;
            }
        }

        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(metaData.Name));
        path = Path.Combine(downloadBase, torrentName);
        _downloadPaths.TryAdd(infoHash, path);
        _completedPaths.TryAdd(infoHash, Path.Combine(completedBase, torrentName));

        var partPath = Path.Combine(path, infoHash.AsString() + ".part");
        _partPaths.TryAdd(infoHash, partPath);

        var metaPath = Path.Combine(path, infoHash.AsString() + ".metadata");
        _metadataPaths.TryAdd(infoHash, metaPath);

        var downloadBitFieldPath = Path.Combine(path, infoHash.AsString() + ".dbf");
        _downloadBitFieldPath.TryAdd(infoHash, downloadBitFieldPath);

        var verificationBitFieldPath = Path.Combine(path, infoHash.AsString() + ".vbf");
        _verificationBitFieldPath.TryAdd(infoHash, verificationBitFieldPath);

        return path;
    }

    private async Task<(string DownloadRoot, string CompletedRoot)> ResolveRootPaths(InfoHash infoHash)
    {
        if (scopeFactory != null)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            var config = await db.Torrents
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.InfoHash == infoHash.AsString());

            if (config != null &&
                !string.IsNullOrWhiteSpace(config.DownloadPath) &&
                !string.IsNullOrWhiteSpace(config.CompletedPath))
            {
                return (config.DownloadPath, config.CompletedPath);
            }
        }

        var settings = await settingsManager.GetFileSettings();
        return (settings.DownloadPath, settings.CompletedPath);
    }
}
