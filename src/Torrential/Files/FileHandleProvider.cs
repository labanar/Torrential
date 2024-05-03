using Microsoft.Extensions.Caching.Memory;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Text.Json;
using Torrential.Torrents;

namespace Torrential.Files;

internal class FileHandleProvider(TorrentMetadataCache metaCache, IMemoryCache cache)
    : IFileHandleProvider
{
    public ConcurrentDictionary<InfoHash, SafeFileHandle> _partFiles = new ConcurrentDictionary<InfoHash, SafeFileHandle>();
    public SemaphoreSlim _creationSemaphore = new SemaphoreSlim(1, 1);

    public async ValueTask<SafeFileHandle> GetPartFileHandle(InfoHash hash)
    {
        if (_partFiles.TryGetValue(hash, out SafeFileHandle handle))
            return handle;

        await _creationSemaphore.WaitAsync();
        if (_partFiles.TryGetValue(hash, out handle))
        {
            _creationSemaphore.Release();
            return handle;
        }

        try
        {
            if (!cache.TryGetValue<FileSettings>("settings.file", out var settings))
                throw new InvalidOperationException("Settings not found");

            if (!metaCache.TryGet(hash, out var meta))
                throw new ArgumentException("Torrent metadata not found in cache");

            var pieceSize = (int)meta.PieceSize;
            var numberOfPieces = meta.NumberOfPieces;
            var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(meta.Name));
            var filePath = Path.Combine(settings.DownloadPath, torrentName, $"{meta.InfoHash.AsString()}.part");
            FileUtilities.TouchFile(filePath, pieceSize * numberOfPieces);
            return _partFiles.GetOrAdd(hash, (torrentId) => File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous));
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    public async ValueTask<SafeFileHandle> GetCompletedFileHandle(InfoHash infoHash, TorrentMetadataFile file)
    {
        if (!cache.TryGetValue<FileSettings>("settings.file", out var settings))
            throw new InvalidOperationException("Settings not found");

        if (!metaCache.TryGet(infoHash, out var meta))
            throw new ArgumentException("Torrent metadata not found in cache");

        //Check if file exists
        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(meta.Name));
        var filePath = Path.Combine(settings.CompletedPath, torrentName, file.Filename);

        FileUtilities.TouchFile(filePath, file.FileSize);
        return File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
    }
}
