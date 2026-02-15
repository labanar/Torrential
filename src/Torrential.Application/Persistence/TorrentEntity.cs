namespace Torrential.Application.Persistence;

public class TorrentEntity
{
    public string InfoHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long PieceSize { get; set; }
    public int NumberOfPieces { get; set; }
    public string FilesJson { get; set; } = string.Empty;
    public string SelectedFileIndicesJson { get; set; } = string.Empty;
    public string AnnounceUrlsJson { get; set; } = string.Empty;
    public byte[] PieceHashes { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset DateAdded { get; set; }
}
