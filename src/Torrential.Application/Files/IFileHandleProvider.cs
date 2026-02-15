using Microsoft.Win32.SafeHandles;
using Torrential.Application.Torrents;

namespace Torrential.Application.Files;

public interface IFileHandleProvider
{
    public ValueTask<SafeFileHandle> GetPartFileHandle(InfoHash infoHash);
    public ValueTask<SafeFileHandle> GetCompletedFileHandle(InfoHash infoHash, TorrentMetadataFile fileName);
    void RemovePartFileHandle(InfoHash hash);
}
