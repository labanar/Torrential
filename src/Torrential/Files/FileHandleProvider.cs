using CommunityToolkit.HighPerformance;
using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Text;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Files;

internal class FileHandleProvider(TorrentMetadataCache metaCache, TorrentFileService fileService, SettingsManager settingsManager)
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
            if (!metaCache.TryGet(hash, out var meta))
                throw new ArgumentException("Torrent metadata not found in cache");

            var pieceSize = (int)meta.PieceSize;
            var numberOfPieces = meta.NumberOfPieces;
            var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(meta.Name));
            var filePath = await fileService.GetPartFilePath(hash);
            FileUtilities.TouchFile(filePath, meta.TotalSize);
            return _partFiles.GetOrAdd(hash, (torrentId) => File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous));
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    public async ValueTask<SafeFileHandle> GetCompletedFileHandle(InfoHash infoHash, TorrentMetadataFile file)
    {
        var settings = await settingsManager.GetFileSettings();

        if (!metaCache.TryGet(infoHash, out var meta))
            throw new ArgumentException("Torrent metadata not found in cache");

        //Check if file exists
        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(meta.Name));
        var safeFileName = GetSafeFilePath(file.Filename);
        var filePath = Path.Combine(settings.CompletedPath, torrentName, safeFileName);

        FileUtilities.TouchFile(filePath, file.FileSize);
        return File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
    }

    private static string GetSafeFilePath(ReadOnlySpan<char> path)
    {
        var pathBuilder = new StringBuilder();
        var invalidPathChars = Path.GetInvalidPathChars();
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        foreach (var pathSegment in path.Tokenize(Path.DirectorySeparatorChar))
        {
            foreach (var c in pathSegment)
            {
                if (invalidPathChars.Contains(c))
                    continue;

                if (invalidFileNameChars.Contains(c))
                    continue;

                pathBuilder.Append(c);
            }

            if (pathBuilder[pathBuilder.Length - 1] == ' ')
                pathBuilder.Remove(pathBuilder.Length - 1, 1);

            pathBuilder.Append(Path.DirectorySeparatorChar);
        }

        //Remove trailing directory separator
        pathBuilder.Remove(pathBuilder.Length - 1, 1);
        return pathBuilder.ToString();
    }
}
