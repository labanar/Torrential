using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using Torrential.Torrents;

namespace Torrential.Files;

internal sealed class ArchiveExtractionService(ILogger<ArchiveExtractionService> logger, IFileHandleProvider fileHandleProvider)
    : IArchiveExtractionService
{
    private static readonly Dictionary<string, string> SupportedArchiveContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".zip"] = "application/zip",
        [".rar"] = "application/vnd.rar",
        [".7z"] = "application/x-7z-compressed"
    };

    public async ValueTask<ArchiveDetectionResult> DetectArchiveAsync(TorrentMetadataFile sourceFile, Stream sourceStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(sourceStream);

        var extension = sourceFile.Extension;
        var extensionMatch = SupportedArchiveContentTypes.TryGetValue(extension, out var contentType);

        var signatureMatch = await MatchesArchiveSignatureAsync(sourceStream, cancellationToken);
        if (extensionMatch || signatureMatch)
            return new ArchiveDetectionResult(true, contentType, extensionMatch ? "supported-extension" : "archive-signature");

        return new ArchiveDetectionResult(false, null, "unsupported-extension");
    }

    public async Task<ArchiveExtractionResult> TryExtractAsync(InfoHash infoHash, TorrentMetadataFile sourceFile, Stream sourceStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(sourceStream);

        if (sourceStream.CanSeek)
            sourceStream.Seek(0, SeekOrigin.Begin);

        var completedRoot = await fileHandleProvider.GetCompletedTorrentPath(infoHash);
        var archiveParent = Path.GetDirectoryName(FileUtilities.GetSafeRelativePath(sourceFile.Filename));

        try
        {
            var extractedFiles = 0;
            using var reader = ReaderFactory.Open(sourceStream, new ReaderOptions { LeaveStreamOpen = true });
            while (reader.MoveToNextEntry())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = reader.Entry;
                if (entry.IsDirectory)
                    continue;

                if (entry.IsEncrypted)
                {
                    logger.LogWarning("Archive {ArchivePath} contains encrypted entries; falling back to raw copy", sourceFile.Filename);
                    return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "encrypted");
                }

                var entryPath = FileUtilities.GetSafeRelativePath(entry.Key ?? string.Empty);
                if (string.IsNullOrWhiteSpace(entryPath))
                {
                    logger.LogWarning("Skipping archive entry with invalid path in {ArchivePath}", sourceFile.Filename);
                    continue;
                }

                var relativeDestination = string.IsNullOrWhiteSpace(archiveParent)
                    ? entryPath
                    : Path.Combine(archiveParent, entryPath);

                if (!FileUtilities.TryResolvePathUnderRoot(completedRoot, relativeDestination, out var destinationPath))
                {
                    logger.LogWarning("Skipping archive entry path traversal attempt in {ArchivePath}: {EntryPath}", sourceFile.Filename, entry.Key);
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    logger.LogWarning("Skipping archive entry with unresolved directory in {ArchivePath}: {EntryPath}", sourceFile.Filename, entry.Key);
                    continue;
                }

                Directory.CreateDirectory(destinationDirectory);
                await using var entryStream = reader.OpenEntryStream();
                await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous);
                await entryStream.CopyToAsync(output, cancellationToken);
                extractedFiles++;
            }

            if (extractedFiles == 0)
            {
                logger.LogWarning("Archive {ArchivePath} had no extractable entries; falling back to raw copy", sourceFile.Filename);
                return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "empty-archive");
            }

            logger.LogInformation("Extracted {FileCount} entries from archive {ArchivePath}", extractedFiles, sourceFile.Filename);
            return new ArchiveExtractionResult(ArchiveExtractionStatus.Extracted, "success");
        }
        catch (Exception ex) when (IsEncryptedArchiveException(ex))
        {
            logger.LogWarning(ex, "Archive {ArchivePath} appears encrypted; falling back to raw copy", sourceFile.Filename);
            return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "encrypted");
        }
        catch (Exception ex) when (IsCorruptArchiveException(ex))
        {
            logger.LogWarning(ex, "Archive {ArchivePath} appears corrupt; falling back to raw copy", sourceFile.Filename);
            return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "corrupt");
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning(ex, "Archive {ArchivePath} uses an unsupported format; falling back to raw copy", sourceFile.Filename);
            return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "unsupported");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Archive {ArchivePath} cannot be processed; falling back to raw copy", sourceFile.Filename);
            return new ArchiveExtractionResult(ArchiveExtractionStatus.FallbackToCopy, "unsupported");
        }
    }

    private static async Task<bool> MatchesArchiveSignatureAsync(Stream sourceStream, CancellationToken cancellationToken)
    {
        if (!sourceStream.CanSeek)
            return false;

        var originalPosition = sourceStream.Position;
        try
        {
            sourceStream.Seek(0, SeekOrigin.Begin);
            var header = new byte[8];
            var bytesRead = await sourceStream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
            return IsZipSignature(header.AsSpan(0, bytesRead)) || IsRarSignature(header.AsSpan(0, bytesRead)) || IsSevenZipSignature(header.AsSpan(0, bytesRead));
        }
        finally
        {
            sourceStream.Seek(originalPosition, SeekOrigin.Begin);
        }
    }

    private static bool IsZipSignature(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B &&
           (bytes[2], bytes[3]) is (0x03, 0x04) or (0x05, 0x06) or (0x07, 0x08);

    private static bool IsRarSignature(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 7)
            return false;

        return bytes[0] == 0x52 &&
               bytes[1] == 0x61 &&
               bytes[2] == 0x72 &&
               bytes[3] == 0x21 &&
               bytes[4] == 0x1A &&
               bytes[5] == 0x07 &&
               (bytes[6] == 0x00 || (bytes.Length >= 8 && bytes[6] == 0x01 && bytes[7] == 0x00));
    }

    private static bool IsSevenZipSignature(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 6 &&
           bytes[0] == 0x37 &&
           bytes[1] == 0x7A &&
           bytes[2] == 0xBC &&
           bytes[3] == 0xAF &&
           bytes[4] == 0x27 &&
           bytes[5] == 0x1C;

    private static bool IsEncryptedArchiveException(Exception ex)
    {
        return ex.Message.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCorruptArchiveException(Exception ex)
    {
        return ex is InvalidFormatException
               || ex is EndOfStreamException
               || ex is IOException;
    }
}
