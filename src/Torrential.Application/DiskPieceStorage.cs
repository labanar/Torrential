using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public class DiskPieceStorage(ISettingsService settingsService, ILogger<DiskPieceStorage> logger) : IPieceStorage
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public async Task InitializeTorrentStorageAsync(TorrentMetaInfo metaInfo)
    {
        var settings = await settingsService.GetSettingsAsync();
        var torrentFolder = GetTorrentFolder(settings.DownloadFolder, metaInfo.Name);
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
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            fs.SetLength(file.FileSize);
        }
    }

    public async Task WritePieceAsync(InfoHash infoHash, int pieceIndex, TorrentMetaInfo metaInfo, AssembledPiece piece)
    {
        var settings = await settingsService.GetSettingsAsync();
        var torrentFolder = GetTorrentFolder(settings.DownloadFolder, metaInfo.Name);

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

            var filePath = GetPartFilePath(torrentFolder, file.FileName);
            // Also check the finalized path in case file was already completed
            if (!File.Exists(filePath))
            {
                var finalPath = GetFinalFilePath(torrentFolder, file.FileName);
                if (File.Exists(finalPath))
                    filePath = finalPath;
            }

            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
            fs.Seek(offsetInFile, SeekOrigin.Begin);
            piece.WriteRangeTo(fs, pieceDataOffset, bytesInFile);

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
        var settings = await settingsService.GetSettingsAsync();
        var torrentFolder = GetTorrentFolder(settings.DownloadFolder, metaInfo.Name);
        var file = metaInfo.Files[fileIndex];

        var partPath = GetPartFilePath(torrentFolder, file.FileName);
        var finalPath = GetFinalFilePath(torrentFolder, file.FileName);

        if (File.Exists(partPath))
        {
            File.Move(partPath, finalPath);
            logger.LogInformation("Finalized file {FileName}", file.FileName);
        }
    }

    private static string GetTorrentFolder(string downloadFolder, string torrentName)
    {
        var sanitized = SanitizePathSegment(torrentName);
        return Path.Combine(downloadFolder, sanitized);
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
}
