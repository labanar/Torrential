using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Torrential.Torrents;

namespace Torrential.Files
{

    public class MetadataFileService(IMemoryCache cache)
        : IMetadataFileService
    {
        public async ValueTask SaveMetadata(TorrentMetadata metaData)
        {
            if (!cache.TryGetValue<FileSettings>("settings.file", out var settings) || settings == null)
                throw new InvalidOperationException("Settings not found");

            var torrentName = Path.GetFileNameWithoutExtension(FileUtilities.GetPathSafeFileName(metaData.Name));
            var filePath = Path.Combine(settings.DownloadPath, torrentName, $"{metaData.InfoHash.AsString()}.metadata");

            FileUtilities.TouchFile(filePath);

            await using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            await JsonSerializer.SerializeAsync(fs, metaData);
        }

        public async IAsyncEnumerable<TorrentMetadata> GetAllMetadataFiles()
        {
            if (!cache.TryGetValue<FileSettings>("settings.file", out var settings) || settings == null)
                throw new InvalidOperationException("Settings not found");

            var inProgressMetas = Directory.GetFiles(settings.DownloadPath, "*.metadata", SearchOption.AllDirectories);
            var completedMetas = Directory.GetFiles(settings.CompletedPath, "*.metadata", SearchOption.AllDirectories);

            foreach (var file in inProgressMetas.Concat(completedMetas))
            {
                await using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var meta = await JsonSerializer.DeserializeAsync<TorrentMetadata>(fs);
                if (meta == null) continue;
                yield return meta;
            }
        }

    }
}
