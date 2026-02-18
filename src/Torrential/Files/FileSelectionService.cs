using System.Collections.Concurrent;
using System.Text.Json;
using Torrential.Settings;

namespace Torrential.Files;

public class FileSelectionService(SettingsManager settingsManager) : IFileSelectionService
{
    private readonly ConcurrentDictionary<InfoHash, HashSet<long>> _cache = new();

    public async Task<IReadOnlySet<long>> GetSelectedFileIds(InfoHash infoHash)
    {
        if (_cache.TryGetValue(infoHash, out var cached))
            return cached;

        var fileIds = await ReadFromDisk(infoHash);
        if (fileIds != null)
        {
            var set = new HashSet<long>(fileIds);
            _cache.TryAdd(infoHash, set);
            return set;
        }

        return new HashSet<long>();
    }

    public async Task SetSelectedFileIds(InfoHash infoHash, IReadOnlyCollection<long> fileIds)
    {
        var set = new HashSet<long>(fileIds);
        _cache[infoHash] = set;
        await WriteToDisk(infoHash, set);
    }

    public void InitializeAllSelected(InfoHash infoHash, IEnumerable<long> allFileIds)
    {
        if (_cache.ContainsKey(infoHash))
            return;

        _cache[infoHash] = new HashSet<long>(allFileIds);
    }

    private async Task<long[]?> ReadFromDisk(InfoHash infoHash)
    {
        var path = await GetSelectionFilePath(infoHash);
        if (!File.Exists(path))
            return null;

        try
        {
            await using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<long[]>(fs);
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteToDisk(InfoHash infoHash, HashSet<long> fileIds)
    {
        var path = await GetSelectionFilePath(infoHash);
        FileUtilities.TouchFile(path);

        await using var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await JsonSerializer.SerializeAsync(fs, fileIds.ToArray());
    }

    private async Task<string> GetSelectionFilePath(InfoHash infoHash)
    {
        var settings = await settingsManager.GetFileSettings();
        var hashString = infoHash.AsString();

        // Search download and completed paths for the matching metadata directory
        var downloadCandidate = FindSelectionFile(settings.DownloadPath, hashString);
        if (downloadCandidate != null)
            return downloadCandidate;

        var completedCandidate = FindSelectionFile(settings.CompletedPath, hashString);
        if (completedCandidate != null)
            return completedCandidate;

        // Default: put it in the download path root
        return Path.Combine(settings.DownloadPath, $"{hashString}.fileselection");
    }

    private static string? FindSelectionFile(string basePath, string hashString)
    {
        try
        {
            var metaFiles = Directory.GetFiles(basePath, $"{hashString}.metadata", SearchOption.AllDirectories);
            if (metaFiles.Length > 0)
            {
                var dir = Path.GetDirectoryName(metaFiles[0])!;
                return Path.Combine(dir, $"{hashString}.fileselection");
            }
        }
        catch
        {
        }

        return null;
    }
}
