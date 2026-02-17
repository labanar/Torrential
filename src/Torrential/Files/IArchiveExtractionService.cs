using Torrential.Torrents;

namespace Torrential.Files;

public interface IArchiveExtractionService
{
    ValueTask<ArchiveDetectionResult> DetectArchiveAsync(TorrentMetadataFile sourceFile, Stream sourceStream, CancellationToken cancellationToken);
    Task<ArchiveExtractionResult> TryExtractAsync(InfoHash infoHash, TorrentMetadataFile sourceFile, Stream sourceStream, CancellationToken cancellationToken);
}

public readonly record struct ArchiveDetectionResult(bool ShouldExtract, string? ContentType, string Reason);

public enum ArchiveExtractionStatus
{
    Extracted,
    FallbackToCopy
}

public readonly record struct ArchiveExtractionResult(ArchiveExtractionStatus Status, string Reason);
