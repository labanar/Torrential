using Microsoft.Win32.SafeHandles;

namespace Torrential.Files
{
    public interface IFileHandleProvider
    {
        public SafeFileHandle GetPartFileHandle(InfoHash infoHash);

        public string GetPartFilePath(InfoHash infoHash);
    }
}
