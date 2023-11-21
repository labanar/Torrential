namespace Torrential.Persistence
{
    internal class Torrent
    {
        public InfoHash Id => InfoHash;
        public required InfoHash InfoHash { get; init; }
        public required string Name { get; init; }
        public long PieceSize { get; init; }
        public int NumberOfPieces { get; init; }
        public ICollection<TorrentFile> Files { get; init; } = Array.Empty<TorrentFile>();

    }

    public class TorrentFile
    {
        public int Id { get; init; }
        public required string Name { get; init; }
        public required long Length { get; init; }
        public required long FirstByteIndex { get; init; }
    }
}
