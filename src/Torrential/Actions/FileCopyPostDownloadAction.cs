using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class FileCopyPostDownloadAction(TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, IArchiveExtractionService archiveExtractionService, TorrentEventBus eventBus, ILogger<FileCopyPostDownloadAction> logger)
            : IPostDownloadAction
    {
        // 1 MB copy buffer - large enough to amortize syscall overhead and leverage OS read-ahead.
        private const int CopyBufferSize = 1 << 20;

        // Defense-in-depth guard to avoid duplicate completion work for the same torrent.
        private static readonly ConcurrentDictionary<InfoHash, byte> _copyInProgress = new();

        public string Name => "FileCopy";
        public bool ContinueOnFailure => false;

        public async Task<PostDownloadActionResult> ExecuteAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            if (!_copyInProgress.TryAdd(infoHash, 1))
            {
                logger.LogWarning("File copy already in progress for torrent {Torrent}; skipping duplicate", infoHash);
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
                foreach (var fileInfo in meta.Files.Where(static file => file.IsSelected))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await eventBus.PublishFileCopyStarted(new TorrentFileCopyStartedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });
                    await MaterializeFileAsync(infoHash, fileInfo, buffer, cancellationToken);
                    await eventBus.PublishFileCopyCompleted(new TorrentFileCopyCompletedEvent { InfoHash = infoHash, FileName = fileInfo.Filename });
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

        private async Task MaterializeFileAsync(InfoHash infoHash, TorrentMetadataFile fileInfo, byte[] buffer, CancellationToken cancellationToken)
        {
            await using var sourceSegmentStream = await fileHandleProvider.OpenPartFileSegmentReadStream(infoHash, fileInfo);
            var detection = await archiveExtractionService.DetectArchiveAsync(fileInfo, sourceSegmentStream, cancellationToken);
            logger.LogInformation(
                "Archive detection for {FileName}: ShouldExtract={ShouldExtract}, Reason={Reason}, ContentType={ContentType}",
                fileInfo.Filename,
                detection.ShouldExtract,
                detection.Reason,
                detection.ContentType ?? "unknown");

            if (!detection.ShouldExtract)
            {
                await CopyFileSegmentToCompletedFileAsync(infoHash, fileInfo, sourceSegmentStream, buffer, cancellationToken);
                return;
            }

            logger.LogInformation("Starting archive extraction for {FileName}", fileInfo.Filename);
            var extractionResult = await archiveExtractionService.TryExtractAsync(infoHash, fileInfo, sourceSegmentStream, cancellationToken);
            if (extractionResult.Status == ArchiveExtractionStatus.Extracted)
            {
                logger.LogInformation("Completed archive extraction for {FileName}", fileInfo.Filename);
                return;
            }

            logger.LogInformation(
                "Archive extraction fallback engaged for {FileName} due to {Reason}; copying raw archive",
                fileInfo.Filename,
                extractionResult.Reason);

            if (sourceSegmentStream.CanSeek)
            {
                sourceSegmentStream.Seek(0, SeekOrigin.Begin);
            }
            else
            {
                logger.LogWarning("Cannot seek source stream for {FileName}; reopening part segment for fallback copy", fileInfo.Filename);
                await using var reopenedSourceSegmentStream = await fileHandleProvider.OpenPartFileSegmentReadStream(infoHash, fileInfo);
                await CopyFileSegmentToCompletedFileAsync(infoHash, fileInfo, reopenedSourceSegmentStream, buffer, cancellationToken);
                return;
            }

            await CopyFileSegmentToCompletedFileAsync(infoHash, fileInfo, sourceSegmentStream, buffer, cancellationToken);
        }

        private async Task CopyFileSegmentToCompletedFileAsync(InfoHash infoHash, TorrentMetadataFile fileInfo, Stream sourceSegmentStream, byte[] buffer, CancellationToken cancellationToken)
        {
            var destinationHandle = await fileHandleProvider.GetCompletedFileHandle(infoHash, fileInfo);
            try
            {
                var writeOffset = 0L;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bytesRead = await sourceSegmentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (bytesRead == 0)
                        break;

                    await RandomAccess.WriteAsync(destinationHandle, buffer.AsMemory(0, bytesRead), writeOffset, cancellationToken);
                    writeOffset += bytesRead;
                }

                RandomAccess.SetLength(destinationHandle, writeOffset);
                if (writeOffset != fileInfo.FileSize)
                {
                    logger.LogWarning(
                        "Copied file {FileName} length mismatch. Expected {ExpectedBytes} bytes but wrote {WrittenBytes} bytes",
                        fileInfo.Filename,
                        fileInfo.FileSize,
                        writeOffset);
                }
            }
            finally
            {
                destinationHandle.Close();
            }
        }
    }
}
