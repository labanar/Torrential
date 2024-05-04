using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Files;

public sealed class TorrentFileService(TorrentMetadataCache metaCache, IMemoryCache cache)
{
    private ConcurrentDictionary<InfoHash, string> _downloadPaths = [];
    private ConcurrentDictionary<InfoHash, string> _partPaths = [];
    private ConcurrentDictionary<InfoHash, string> _metadataPaths = [];
    private ConcurrentDictionary<InfoHash, string> _downloadBitFieldPath = [];
    private ConcurrentDictionary<InfoHash, string> _verificationBitFieldPath = [];

    public string GetMetadataFilePath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            BuildPathCache(infoHash);

        return _metadataPaths[infoHash];
    }

    public string GetDownloadBitFieldPath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            BuildPathCache(infoHash);

        return _downloadBitFieldPath[infoHash];
    }

    public string GetVerificationBitFieldPath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            BuildPathCache(infoHash);

        return _verificationBitFieldPath[infoHash];
    }

    public string GetPartFilePath(InfoHash infoHash)
    {
        if (!_downloadPaths.ContainsKey(infoHash))
            BuildPathCache(infoHash);

        return _partPaths[infoHash];
    }

    private string BuildPathCache(InfoHash infoHash)
    {
        if (_downloadPaths.TryGetValue(infoHash, out var path))
            return path;

        if (!metaCache.TryGet(infoHash, out var metaData))
            throw new InvalidOperationException("Torrent metadata not found");


        if (!cache.TryGetValue<FileSettings>("settings.file", out var settings))
            throw new InvalidOperationException("Settings not found");


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
