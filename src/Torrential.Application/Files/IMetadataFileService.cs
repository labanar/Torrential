using Torrential.Application.Torrents;

namespace Torrential.Application.Files;

public interface IMetadataFileService
{
    ValueTask SaveMetadata(TorrentMetadata metaData);
    IAsyncEnumerable<TorrentMetadata> GetAllMetadataFiles();
}
