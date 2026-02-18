using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using Torrential.Torrents;

namespace Torrential.Files;

internal class FileHandleProvider(TorrentMetadataCache metaCache, TorrentFileService fileService)
    : IFileHandleProvider
{
    public ConcurrentDictionary<InfoHash, SafeFileHandle> _partFiles = new();
    public SemaphoreSlim _creationSemaphore = new(1, 1);

    public void RemovePartFileHandle(InfoHash hash)
    {
        if (!_partFiles.TryRemove(hash, out var handle))
            return;

        handle?.Close();
    }

    public async ValueTask<SafeFileHandle> GetPartFileHandle(InfoHash hash)
    {
        if (_partFiles.TryGetValue(hash, out var handle) && handle != null)
            return handle;

        await _creationSemaphore.WaitAsync();
        if (_partFiles.TryGetValue(hash, out handle))
        {
            _creationSemaphore.Release();
            return handle;
        }

        try
        {
            if (!metaCache.TryGet(hash, out var meta))
                throw new ArgumentException("Torrent metadata not found in cache");

            var filePath = await fileService.GetPartFilePath(hash);
            FileUtilities.TouchFile(filePath, meta.TotalSize);
            return _partFiles.GetOrAdd(hash, _ => File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous));
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    public async ValueTask<SafeFileHandle> GetCompletedFileHandle(InfoHash infoHash, TorrentMetadataFile file)
    {
        var completedPath = await GetCompletedTorrentPath(infoHash);
        var safeFileName = FileUtilities.GetSafeRelativePath(file.Filename);
        var filePath = Path.Combine(completedPath, safeFileName);

        FileUtilities.TouchFile(filePath, file.FileSize);
        return File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
    }

    public async Task<string> GetCompletedTorrentPath(InfoHash infoHash)
    {
        if (!metaCache.TryGet(infoHash, out _))
            throw new ArgumentException("Torrent metadata not found in cache");

        var completedPath = await fileService.GetCompletedTorrentPath(infoHash);
        Directory.CreateDirectory(completedPath);
        return completedPath;
    }

    public async ValueTask<Stream> OpenPartFileSegmentReadStream(InfoHash infoHash, TorrentMetadataFile file, bool leaveOpen = true)
    {
        var partFileHandle = await GetPartFileHandle(infoHash);
        return new PartFileSegmentReadStream(partFileHandle, file.FileStartByte, file.FileSize, leaveOpen);
    }
}
