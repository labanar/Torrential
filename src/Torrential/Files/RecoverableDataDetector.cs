using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Files;

public sealed class RecoverableDataResult
{
    public required bool HasRecoverableData { get; init; }
    public long PartFileLength { get; init; }
    public required string Reason { get; init; }
}

public sealed class RecoverableDataDetector(SettingsManager settingsManager)
{
    private static readonly HashSet<string> BookkeepingExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".metadata",
        ".dbf",
        ".vbf",
        ".fileselection"
    };

    /// <summary>
    /// Inspects the torrent's incomplete directory for pre-existing data that could be salvaged.
    /// This must be called BEFORE any file-creating side effects (metadata save, part file creation)
    /// so it only detects truly pre-existing data.
    /// </summary>
    public async Task<RecoverableDataResult> Detect(TorrentMetadata metadata)
    {
        var settings = await settingsManager.GetFileSettings();
        var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(metadata.Name));
        var downloadDir = Path.Combine(settings.DownloadPath, torrentName);

        if (!Directory.Exists(downloadDir))
        {
            return new RecoverableDataResult
            {
                HasRecoverableData = false,
                Reason = "Download directory does not exist"
            };
        }

        var partFileName = $"{metadata.InfoHash.AsString()}.part";
        var partFilePath = Path.Combine(downloadDir, partFileName);

        if (!File.Exists(partFilePath))
        {
            // Check if the directory contains only bookkeeping files (no meaningful data)
            var hasNonBookkeepingFiles = HasNonBookkeepingFiles(downloadDir);
            return new RecoverableDataResult
            {
                HasRecoverableData = false,
                Reason = hasNonBookkeepingFiles
                    ? "Part file not found but directory contains other files"
                    : "Part file not found"
            };
        }

        var fileInfo = new FileInfo(partFilePath);
        if (fileInfo.Length == 0)
        {
            return new RecoverableDataResult
            {
                HasRecoverableData = false,
                PartFileLength = 0,
                Reason = "Part file exists but is empty"
            };
        }

        return new RecoverableDataResult
        {
            HasRecoverableData = true,
            PartFileLength = fileInfo.Length,
            Reason = $"Part file found with {fileInfo.Length} bytes (expected {metadata.TotalSize})"
        };
    }

    private static bool HasNonBookkeepingFiles(string directoryPath)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directoryPath))
            {
                var extension = Path.GetExtension(file);
                if (!BookkeepingExtensions.Contains(extension))
                    return true;
            }
        }
        catch
        {
            // If we can't enumerate, assume no meaningful files
        }

        return false;
    }
}
