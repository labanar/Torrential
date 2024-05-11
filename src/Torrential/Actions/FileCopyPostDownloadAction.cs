using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class FileCopyPostDownloadAction(TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, IBus bus, ILogger<FileCopyPostDownloadAction> logger)
            : IPostDownloadAction
    {
        public string Name => "FileCopy";
        public bool ContinueOnFailure => false;

        public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return new PostDownloadActionResult { Success = false };

            foreach (var fileInfo in meta.Files)
            {
                var offset = fileInfo.FileStartByte;
                var fileEnd = fileInfo.FileStartByte + fileInfo.FileSize;

                var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Pow(2, 14));
                try
                {
                    var partFileHandle = await fileHandleProvider.GetPartFileHandle(infoHash);
                    var destinationHandle = await fileHandleProvider.GetCompletedFileHandle(infoHash, fileInfo);
                    await bus.Publish(new TorrentFileCopyStartedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });

                    while (offset < fileEnd)
                    {
                        var writeOffset = offset - fileInfo.FileStartByte;
                        var read = RandomAccess.Read(partFileHandle, buffer, offset);
                        RandomAccess.Write(destinationHandle, buffer, offset - fileInfo.FileStartByte); // Fix: Write from buffer index 0
                        offset += read;
                    }

                    //We may write additonal bytes, so we truncate the file to the correct size
                    RandomAccess.SetLength(destinationHandle, fileInfo.FileSize);
                    destinationHandle.Close();
                    await bus.Publish(new TorrentFileCopyCompletedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error copying file {File} for torrent {Torrent}", fileInfo.Filename, infoHash);
                    return new PostDownloadActionResult { Success = false };
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            return new PostDownloadActionResult { Success = true };
        }
    }
}
