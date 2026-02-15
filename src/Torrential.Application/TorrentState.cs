using Torrential.Core;

namespace Torrential.Application;

public sealed class TorrentState
{
    public required InfoHash InfoHash { get; init; }
    public required string Name { get; init; }
    public required long TotalSize { get; init; }
    public required long PieceSize { get; init; }
    public required int NumberOfPieces { get; init; }
    public required IReadOnlyList<TorrentFileInfo> Files { get; init; }
    public required IReadOnlySet<int> SelectedFileIndices { get; set; }
    public TorrentStatus Status { get; set; } = TorrentStatus.Added;
    public DateTimeOffset DateAdded { get; init; } = DateTimeOffset.UtcNow;
}
