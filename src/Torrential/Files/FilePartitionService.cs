using MassTransit;
using System.Buffers;
using System.Text;
using Torrential.Torrents;

namespace Torrential.Files
{
    /// <summary>
    /// After a torrent is completed, we need to take the .part file and split it into the files that the torrent is made up of.
    /// </summary>
    /// <param name="metaCache"></param>
    public class FilePartitionService(TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, IBus bus)
        : IConsumer<TorrentCompleteEvent>

    {
        public async Task Consume(ConsumeContext<TorrentCompleteEvent> context)
        {
            await CopyToCompletedDestination(context.Message.InfoHash);
        }

        public async Task CopyToCompletedDestination(InfoHash infoHash)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return;

            foreach (var fileInfo in meta.Files)
            {
                var offset = fileInfo.FileStartByte;
                var fileEnd = fileInfo.FileStartByte + fileInfo.FileSize;
                var partFilePath = fileHandleProvider.GetPartFilePath(infoHash);
                var torrentName = Path.GetFileNameWithoutExtension(GetPathSafeFileName(meta.Name));

                var destinationDir = Path.Combine(CompletedDirectory(), torrentName);
                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                var destinationFileName = Path.Combine(destinationDir, GetPathSafeFileName(fileInfo.Filename));
                var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Pow(2, 14));
                try
                {
                    var partFileHandle = fileHandleProvider.GetPartFileHandle(infoHash);
                    TouchFile(destinationFileName);
                    var destinationHandle = File.OpenHandle(destinationFileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);

                    await bus.Publish(new TorrentFileCopyStartedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });

                    while (offset < fileEnd)
                    {
                        var read = RandomAccess.Read(partFileHandle, buffer, offset);
                        RandomAccess.Write(destinationHandle, buffer, offset);
                        offset += read;
                    }

                    RandomAccess.SetLength(destinationHandle, fileInfo.FileSize);
                    await bus.Publish(new TorrentFileCopyCompletedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public static FileInfo TouchFile(string path)
        {

            if (File.Exists(path))
                return new FileInfo(path);

            using var fs = File.Create(path);
            fs.Close();

            return new FileInfo(path);
        }


        //TODO - lift this up to config
        public static string CompletedDirectory()
        {
            var tempFilePath = Path.GetTempPath();
            var completedDirectory = Path.Combine(tempFilePath, "torrential", "completed");

            if (!Directory.Exists(completedDirectory))
                Directory.CreateDirectory(completedDirectory);

            return completedDirectory;
        }

        public static string GetPathSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var fileNameBuilder = new StringBuilder();
            foreach (var c in fileName)
            {
                if (invalidChars.Contains(c)) continue;
                fileNameBuilder.Append(c);
            }
            return fileName.ToString();
        }
    }
}
