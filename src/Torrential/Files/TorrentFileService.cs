using System.Collections.Concurrent;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Files;

public sealed class TorrentFileService(TorrentMetadataCache metaCache, SettingsManager settingsManager)
{
    private ConcurrentDictionary<InfoHash, string> _downloadPaths = [];
    private ConcurrentDictionary<InfoHash, string> _partPaths = [];
    private ConcurrentDictionary<InfoHash, string> _metadataPaths = [];
    private ConcurrentDictionary<InfoHash, string> _downloadBitFieldPath = [];
    private ConcurrentDictionary<InfoHash, string> _verificationBitFieldPath = [];

    public Task ClearData(InfoHash infoHash)
    {
        _downloadBitFieldPath.TryRemove(infoHash, out _);
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

    private async Task<string> BuildPathCache(InfoHash infoHash)
    {
        if (_downloadPaths.TryGetValue(infoHash, out var path))
            return path;

        if (!metaCache.TryGet(infoHash, out var metaData))
            throw new InvalidOperationException("Torrent metadata not found");

        var settings = await settingsManager.GetFileSettings();

        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(metaData.Name));
        path = Path.Combine(settings.DownloadPath, torrentName);
        _downloadPaths.TryAdd(infoHash, path);

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
}
