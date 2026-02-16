using MassTransit;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class FileCopyPostDownloadAction(TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, IBus bus, ILogger<FileCopyPostDownloadAction> logger)
            : IPostDownloadAction
    {
        // 1 MB copy buffer — large enough to amortize syscall overhead and leverage OS read-ahead,
        // small enough to stay well under the 85 KB LOH threshold per-element (the array itself is 1 MB
        // so it lands on the LOH, but ArrayPool reuses it across calls so no repeated allocation pressure).
        private const int CopyBufferSize = 1 << 20; // 1,048,576 bytes

        // Defense-in-depth: prevents concurrent file copy operations for the same torrent.
        // Even though PieceValidator now guarantees exactly-once TorrentCompleteEvent publish,
        // this guard protects against any future code path that might trigger a duplicate.
        private static readonly ConcurrentDictionary<InfoHash, byte> _copyInProgress = new();

        public string Name => "FileCopy";
        public bool ContinueOnFailure => false;

        public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            // Atomic guard: only one copy operation per torrent at a time.
            if (!_copyInProgress.TryAdd(infoHash, 1))
            {
                logger.LogWarning("File copy already in progress for torrent {Torrent} — skipping duplicate", infoHash);
                return new PostDownloadActionResult { Success = true };
            }

            try
            {
                return await ExecuteCopyAsync(infoHash, cancellationToken);
            }
            finally
            {
                _copyInProgress.TryRemove(infoHash, out _);
            }
        }

        private async Task<PostDownloadActionResult> ExecuteCopyAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            if (!metaCache.TryGet(infoHash, out var meta))
                return new PostDownloadActionResult { Success = false };

            var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
            try
            {
                var partFileHandle = await fileHandleProvider.GetPartFileHandle(infoHash);

                foreach (var fileInfo in meta.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var readOffset = fileInfo.FileStartByte;
                    var fileEnd = fileInfo.FileStartByte + fileInfo.FileSize;
                    var writeOffset = 0L;

                    var destinationHandle = await fileHandleProvider.GetCompletedFileHandle(infoHash, fileInfo);
                    try
                    {
                        await bus.Publish(new TorrentFileCopyStartedEvent { InfoHash = infoHash, FileName = fileInfo.Filename }, cancellationToken);

                        while (readOffset < fileEnd)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Limit read to remaining bytes so we do not overshoot the file boundary.
                            var remaining = fileEnd - readOffset;
                            var bytesToRead = (int)Math.Min(remaining, buffer.Length);
                            var mem = buffer.AsMemory(0, bytesToRead);

                            var bytesRead = await RandomAccess.ReadAsync(partFileHandle, mem, readOffset, cancellationToken);
                            if (bytesRead == 0)
                                break; // EOF — should not happen for a valid part file, but guard against infinite loop

                            await RandomAccess.WriteAsync(destinationHandle, buffer.AsMemory(0, bytesRead), writeOffset, cancellationToken);

                            readOffset += bytesRead;
                            writeOffset += bytesRead;
                        }

                        // The rented buffer may be larger than requested, so the final read/write could
                        // overshoot. Truncate to exact file size.
                        RandomAccess.SetLength(destinationHandle, fileInfo.FileSize);
                        await bus.Publish(new TorrentFileCopyCompletedEvent { InfoHash = infoHash, FileName = fileInfo.Filename }, cancellationToken);
                    }
                    finally
                    {
                        destinationHandle.Close();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("File copy cancelled for torrent {Torrent}", infoHash);
                return new PostDownloadActionResult { Success = false };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error copying files for torrent {Torrent}", infoHash);
                return new PostDownloadActionResult { Success = false };
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return new PostDownloadActionResult { Success = true };
        }
    }
}
