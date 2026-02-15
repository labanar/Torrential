using Torrential.Core;

namespace Torrential.Application;

public sealed class TorrentMetaInfo
{
    public required InfoHash InfoHash { get; init; }
    public required string Name { get; init; }
    public required long TotalSize { get; init; }
    public required long PieceSize { get; init; }
    public required int NumberOfPieces { get; init; }
    public required IReadOnlyList<TorrentFileInfo> Files { get; init; }
    public required IReadOnlyList<string> AnnounceUrls { get; init; }
    public required byte[] PieceHashes { get; init; }
}
