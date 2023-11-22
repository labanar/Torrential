using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.Text;

namespace Torrential.Files
{
    internal class FileHandleProvider(TorrentMetadataCache metaCache)
        : IFileHandleProvider
    {
        private ConcurrentDictionary<InfoHash, SafeFileHandle> _fileHandles = new ConcurrentDictionary<InfoHash, SafeFileHandle>();
        private SemaphoreSlim _fileCreationSemaphore = new SemaphoreSlim(1, 1);


        public SafeFileHandle GetFileHandle(InfoHash hash)
        {
            if (_fileHandles.TryGetValue(hash, out SafeFileHandle handle))
                return handle;

            _fileCreationSemaphore.Wait();

            if (_fileHandles.TryGetValue(hash, out handle))
            {
                _fileCreationSemaphore.Release();
                return handle;
            }

            try
            {
                var fileName = $"{Math.Abs(hash.P1)}_{Math.Abs(hash.P2)}_{Math.Abs(hash.P3)}";
                if (!metaCache.TryGet(hash, out var meta))
                {
                    //Not ideal to throw here, but doing it for now
                    throw new ArgumentException("Torrent metadata not found in cache");
                }
                var pieceSize = (int)meta.PieceSize;
                var numberOfPieces = meta.NumberOfPieces;
                var filePath = CreateTorrentPartFile(fileName, pieceSize, numberOfPieces);
                return _fileHandles.GetOrAdd(hash, (torrentId) => File.OpenHandle(filePath.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous));
            }
            finally
            {
                _fileCreationSemaphore.Release();
            }
        }

        public static FileInfo CreateTorrentPartFile(string torrentId, int pieceSize, int numberOfPieces)
        {
            var tempFilePath = Path.GetTempPath();

            var invalidChars = Path.GetInvalidFileNameChars();
            var fileNameBuilder = new StringBuilder();
            foreach (var c in torrentId)
            {
                if (invalidChars.Contains(c)) continue;
                fileNameBuilder.Append(c);
            }

            fileNameBuilder.Append(".part");
            var filePath = Path.Combine(tempFilePath, fileNameBuilder.ToString());

            if (File.Exists(filePath))
                return new FileInfo(filePath);

            using var fs = File.Create(filePath);
            fs.SetLength(pieceSize * numberOfPieces);
            fs.Close();

            return new FileInfo(filePath);
        }
    }
}
