using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using Torrential.Core;

namespace Torrential.Application;

public class DiskPieceStorage(ISettingsService settingsService, ILogger<DiskPieceStorage> logger) : IPieceStorage, IDisposable
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    // Cache file handles per torrent+file to avoid repeated open/close
    private readonly ConcurrentDictionary<(InfoHash, int), SafeFileHandle> _fileHandles = new();
    private readonly SemaphoreSlim _handleSemaphore = new(1, 1);
    private string? _downloadFolder;

    public async Task InitializeTorrentStorageAsync(TorrentMetaInfo metaInfo)
    {
        var torrentFolder = await GetTorrentFolder(metaInfo.Name);
        Directory.CreateDirectory(torrentFolder);

        foreach (var file in metaInfo.Files)
        {
            var filePath = GetPartFilePath(torrentFolder, file.FileName);
            var directory = Path.GetDirectoryName(filePath);
            if (directory is not null)
                Directory.CreateDirectory(directory);

            if (File.Exists(filePath))
            {
                var existingLength = new FileInfo(filePath).Length;
                if (existingLength == file.FileSize)
                {
                    logger.LogDebug("File {FileName} already pre-allocated, skipping", file.FileName);
                    continue;
                }
            }

            logger.LogInformation("Pre-allocating {FileName} ({FileSize} bytes)", file.FileName, file.FileSize);
            using var fs = File.Create(filePath);
            fs.SetLength(file.FileSize);
        }
    }

    public async Task WritePieceAsync(InfoHash infoHash, int pieceIndex, TorrentMetaInfo metaInfo, AssembledPiece piece)
    {
        var torrentFolder = await GetTorrentFolder(metaInfo.Name);

        var pieceOffset = (long)pieceIndex * metaInfo.PieceSize;
        var isLastPiece = pieceIndex == metaInfo.NumberOfPieces - 1;
        var pieceLength = isLastPiece
            ? (int)(metaInfo.TotalSize - pieceOffset)
            : (int)metaInfo.PieceSize;

        var globalOffset = pieceOffset;
        var bytesRemaining = pieceLength;
        var pieceDataOffset = 0;

        // Walk through files to find which ones this piece overlaps
        long fileStart = 0;
        foreach (var file in metaInfo.Files)
        {
            var fileEnd = fileStart + file.FileSize;

            if (globalOffset >= fileEnd)
            {
                fileStart = fileEnd;
                continue;
            }

            if (globalOffset + bytesRemaining <= fileStart)
                break;

            var offsetInFile = globalOffset - fileStart;
            var bytesInFile = (int)Math.Min(bytesRemaining, fileEnd - globalOffset);

            var handle = await GetOrCreateFileHandle(infoHash, file.FileIndex, torrentFolder, file.FileName);
            piece.WriteRangeTo(handle, offsetInFile, pieceDataOffset, bytesInFile);

            logger.LogDebug("Wrote {Bytes} bytes to {File} at offset {Offset} for piece {PieceIndex}",
                bytesInFile, file.FileName, offsetInFile, pieceIndex);

            pieceDataOffset += bytesInFile;
            bytesRemaining -= bytesInFile;
            globalOffset += bytesInFile;
            fileStart = fileEnd;

            if (bytesRemaining <= 0)
                break;
        }
    }

    public bool IsFileComplete(InfoHash infoHash, int fileIndex, TorrentMetaInfo metaInfo, Bitfield localBitfield)
    {
        var file = metaInfo.Files[fileIndex];
        var (firstPiece, lastPiece) = GetPieceRangeForFile(metaInfo, file);

        for (var i = firstPiece; i <= lastPiece; i++)
        {
            if (!localBitfield.HasPiece(i))
                return false;
        }

        return true;
    }

    public async Task FinalizeFileAsync(InfoHash infoHash, int fileIndex, TorrentMetaInfo metaInfo)
    {
        var torrentFolder = await GetTorrentFolder(metaInfo.Name);
        var file = metaInfo.Files[fileIndex];

        // Close and remove the cached handle before renaming
        if (_fileHandles.TryRemove((infoHash, fileIndex), out var handle))
            handle.Close();

        var partPath = GetPartFilePath(torrentFolder, file.FileName);
        var finalPath = GetFinalFilePath(torrentFolder, file.FileName);

        if (File.Exists(partPath))
        {
            File.Move(partPath, finalPath);
            logger.LogInformation("Finalized file {FileName}", file.FileName);
        }
    }

    private async Task<SafeFileHandle> GetOrCreateFileHandle(InfoHash infoHash, int fileIndex, string torrentFolder, string fileName)
    {
        var key = (infoHash, fileIndex);
        if (_fileHandles.TryGetValue(key, out var existing))
            return existing;

        await _handleSemaphore.WaitAsync();
        try
        {
            if (_fileHandles.TryGetValue(key, out existing))
                return existing;

            var filePath = GetPartFilePath(torrentFolder, fileName);
            if (!File.Exists(filePath))
            {
                var finalPath = GetFinalFilePath(torrentFolder, fileName);
                if (File.Exists(finalPath))
                    filePath = finalPath;
            }

            var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
            _fileHandles.TryAdd(key, handle);
            return handle;
        }
        finally
        {
            _handleSemaphore.Release();
        }
    }

    private async Task<string> GetTorrentFolder(string torrentName)
    {
        if (_downloadFolder is null)
        {
            var settings = await settingsService.GetSettingsAsync();
            _downloadFolder = settings.DownloadFolder;
        }

        var sanitized = SanitizePathSegment(torrentName);
        return Path.Combine(_downloadFolder, sanitized);
    }

    private static string GetPartFilePath(string torrentFolder, string fileName)
    {
        var sanitizedPath = SanitizeFilePath(fileName);
        return Path.Combine(torrentFolder, sanitizedPath + ".part");
    }

    private static string GetFinalFilePath(string torrentFolder, string fileName)
    {
        var sanitizedPath = SanitizeFilePath(fileName);
        return Path.Combine(torrentFolder, sanitizedPath);
    }

    private static string SanitizeFilePath(string filePath)
    {
        var segments = filePath.Split('/', '\\');
        return Path.Combine(segments.Select(SanitizePathSegment).ToArray());
    }

    private static string SanitizePathSegment(string segment)
    {
        foreach (var c in InvalidFileNameChars)
            segment = segment.Replace(c, '_');
        return segment;
    }

    private static (int FirstPiece, int LastPiece) GetPieceRangeForFile(TorrentMetaInfo metaInfo, TorrentFileInfo file)
    {
        long fileStart = 0;
        for (var i = 0; i < file.FileIndex; i++)
            fileStart += metaInfo.Files[i].FileSize;

        var fileEnd = fileStart + file.FileSize - 1;
        var firstPiece = (int)(fileStart / metaInfo.PieceSize);
        var lastPiece = (int)(fileEnd / metaInfo.PieceSize);
        return (firstPiece, lastPiece);
    }

    public void Dispose()
    {
        foreach (var handle in _fileHandles.Values)
            handle.Close();
        _fileHandles.Clear();
        _handleSemaphore.Dispose();
    }
}
