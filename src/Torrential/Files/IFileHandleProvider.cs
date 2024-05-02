using Microsoft.Win32.SafeHandles;
using Torrential.Torrents;

namespace Torrential.Files
{
    public interface IFileHandleProvider
    {
        public ValueTask<SafeFileHandle> GetPartFileHandle(InfoHash infoHash);
        public ValueTask<SafeFileHandle> GetCompletedFileHandle(InfoHash infoHash, TorrentMetadataFile fileName);
    }
}
