using Microsoft.Win32.SafeHandles;

namespace Torrential.Files
{
    public interface IFileHandleProvider
    {
        public SafeFileHandle GetFileHandle(InfoHash infoHash);
    }
}
