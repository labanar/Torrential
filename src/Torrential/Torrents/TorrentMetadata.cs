using System.Text.Json.Serialization;

namespace Torrential.Torrents;

public class TorrentMetadata
{
    private const int STANDARD_CHUNK_SIZE = 16384; // 16 KB per chunk
    public required string Name { get; init; }
    public string[] UrlList { get; init; } = Array.Empty<string>();
    public required ICollection<string> AnnounceList { get; set; } = Array.Empty<string>();
    public required ICollection<TorrentMetadataFile> Files { get; set; } = Array.Empty<TorrentMetadataFile>();
    public required long PieceSize { get; set; }
    public required InfoHash InfoHash { get; set; }
    public required long TotalSize { get; set; }

    private byte[] _pieceHashesConcat = Array.Empty<byte>();
    private int _numberOfPieces = 0;

    [JsonIgnore]
    private HashSet<int>? _wantedPieces;

    public int NumberOfPieces => _numberOfPieces;

    [JsonIgnore]
    public IReadOnlySet<int> WantedPieces => _wantedPieces ??= BuildWantedPieceSet();

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
        }
    }

    public int FinalPieceSize
    {
        get
        {
            if (_numberOfPieces == 0) return 0;
            var totalSizeOfAllButLastPiece = (_numberOfPieces - 1) * PieceSize;
            var lastPieceSize = TotalSize - totalSizeOfAllButLastPiece;
            return (int)lastPieceSize;
        }
    }

    public int TotalNumberOfChunks
    {
        get
        {
            var fullPieceChunks = (int)(PieceSize / STANDARD_CHUNK_SIZE);
            var totalFullPieces = NumberOfPieces - 1;
            var totalChunksForFullPieces = totalFullPieces * fullPieceChunks;
            var finalPieceChunks = (FinalPieceSize + STANDARD_CHUNK_SIZE - 1) / STANDARD_CHUNK_SIZE;
            return totalChunksForFullPieces + finalPieceChunks;
        }
    }

    public ReadOnlySpan<byte> GetPieceHash(int pieceIndex)
    {
        var offset = pieceIndex * 20;
        return _pieceHashesConcat.AsSpan().Slice(offset, 20);
    }

    public bool IsPieceWanted(int pieceIndex)
    {
        return WantedPieces.Contains(pieceIndex);
    }

    private HashSet<int> BuildWantedPieceSet()
    {
        var wanted = new HashSet<int>();
        if (PieceSize <= 0 || NumberOfPieces <= 0)
            return wanted;

        foreach (var file in Files.Where(static f => f.IsSelected && f.FileSize > 0))
        {
            var startPiece = (int)(file.FileStartByte / PieceSize);
            var endByteInclusive = file.FileStartByte + file.FileSize - 1;
            var endPiece = (int)(endByteInclusive / PieceSize);
            startPiece = Math.Clamp(startPiece, 0, NumberOfPieces - 1);
            endPiece = Math.Clamp(endPiece, 0, NumberOfPieces - 1);

            for (var pieceIndex = startPiece; pieceIndex <= endPiece; pieceIndex++)
                wanted.Add(pieceIndex);
        }

        return wanted;
    }
}

public class TorrentMetadataFile
{
    public required long Id { get; set; }
    public required string Filename { get; set; }
    public required long FileStartByte { get; set; }
    public required long FileSize { get; set; }
    public bool IsSelected { get; set; } = true;
}
