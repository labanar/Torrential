using Microsoft.Extensions.Logging;
using System.Buffers;
using Torrential.Application.Events;
using Torrential.Application.Files;
using Torrential.Application.Torrents;

namespace Torrential.Application.FileCopy;

public class FileCopyService(TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, IEventBus eventBus, ILogger<FileCopyService> logger)
    : IFileCopyService
{
    public async Task CopyFilesAsync(InfoHash infoHash, CancellationToken cancellationToken = default)
    {
        if (!metaCache.TryGet(infoHash, out var meta))
            return;

        foreach (var fileInfo in meta.Files)
        {
            var offset = fileInfo.FileStartByte;
            var fileEnd = fileInfo.FileStartByte + fileInfo.FileSize;

            var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Pow(2, 14));
            try
            {
                var partFileHandle = await fileHandleProvider.GetPartFileHandle(infoHash);
                var destinationHandle = await fileHandleProvider.GetCompletedFileHandle(infoHash, fileInfo);
                await eventBus.PublishAsync(new TorrentFileCopyStartedEvent { InfoHash = infoHash, FileName = fileInfo.Filename }, cancellationToken);

                while (offset < fileEnd)
                {
                    var read = RandomAccess.Read(partFileHandle, buffer, offset);
                    RandomAccess.Write(destinationHandle, buffer, offset - fileInfo.FileStartByte);
                    offset += read;
                }

                //We may write additional bytes, so we truncate the file to the correct size
                RandomAccess.SetLength(destinationHandle, fileInfo.FileSize);
                destinationHandle.Close();
                await eventBus.PublishAsync(new TorrentFileCopyCompletedEvent { InfoHash = infoHash, FileName = fileInfo.Filename }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error copying file {File} for torrent {Torrent}", fileInfo.Filename, infoHash);
                return;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
