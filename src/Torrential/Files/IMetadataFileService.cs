using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IMetadataFileService
    {
        ValueTask SaveMetadata(TorrentMetadata metaData);
        IAsyncEnumerable<TorrentMetadata> GetAllMetadataFiles();
    }
}
