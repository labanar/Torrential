namespace Torrential.Torrents;

public class TorrentMetadata
{
    public required string Name { get; init; }
    public string[] UrlList { get; init; } = Array.Empty<string>();
    public required ICollection<string> AnnounceList { get; set; } = Array.Empty<string>();
    public required ICollection<TorrentMetadataFile> Files { get; set; } = Array.Empty<TorrentMetadataFile>();
    public required long PieceSize { get; set; }
    public required InfoHash InfoHash { get; set; }

    private byte[] _pieceHashesConcat = Array.Empty<byte>();
    private int _numberOfPieces = 0;

    public int NumberOfPieces => _numberOfPieces;
    public required byte[] PieceHashesConcatenated
    {
        get
        {
            return _pieceHashesConcat;
        }
        set
        {
            _pieceHashesConcat = value;
            _numberOfPieces = value.Length / 20;
            _numberOfPieces += value.Length % 20 == 0 ? 0 : 1;
        }
    }

    public ReadOnlySpan<byte> GetPieceHash(int pieceIndex)
    {
        var offset = pieceIndex * 20;
        return _pieceHashesConcat.AsSpan().Slice(offset, 20);
    }
}

public class TorrentMetadataFile
{
    public required long Id { get; set; }
    public required string Filename { get; set; }
    public required long FileStartByte { get; set; }
    public required long FileSize { get; set; }
}