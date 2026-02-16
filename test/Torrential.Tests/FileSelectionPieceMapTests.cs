using Torrential.Files;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Tests;

public class FileSelectionPieceMapTests
{
    /// <summary>
    /// Stub IFileSelectionService backed by an in-memory dictionary.
    /// </summary>
    private sealed class StubFileSelectionService : IFileSelectionService
    {
        private readonly Dictionary<string, HashSet<long>> _store = new();

        public Task<IReadOnlySet<long>> GetSelectedFileIds(InfoHash infoHash)
        {
            if (_store.TryGetValue(infoHash.AsString(), out var set))
                return Task.FromResult<IReadOnlySet<long>>(set);
            return Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());
        }

        public Task SetSelectedFileIds(InfoHash infoHash, IReadOnlyCollection<long> fileIds)
        {
            _store[infoHash.AsString()] = new HashSet<long>(fileIds);
            return Task.CompletedTask;
        }
    }

    private static InfoHash TestInfoHash => InfoHash.FromHexString("0102030405060708091011121314151617181920");

    private static TorrentMetadata MakeMetadata(int numberOfPieces, long pieceSize, params TorrentMetadataFile[] files)
    {
        return new TorrentMetadata
        {
            Name = "test",
            InfoHash = TestInfoHash,
            PieceSize = pieceSize,
            TotalSize = numberOfPieces * pieceSize,
            AnnounceList = Array.Empty<string>(),
            Files = files,
            PieceHashesConcatenated = new byte[numberOfPieces * 20]
        };
    }

    [Fact]
    public async Task AllFilesSelected_RemovesFilter()
    {
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 1000 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 1000, FileSize = 1000 }
        };
        var meta = MakeMetadata(numberOfPieces: 4, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 1, 2 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        // All files selected → no filter (null)
        Assert.Null(pieceMap.GetAllowedPieces(TestInfoHash));
    }

    [Fact]
    public async Task NoFilesSelected_BlocksAllPieces()
    {
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 1000 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 1000, FileSize = 1000 }
        };
        var meta = MakeMetadata(numberOfPieces: 4, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        // Empty selection
        await fileSelection.SetSelectedFileIds(TestInfoHash, Array.Empty<long>());

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        var allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        Assert.True(allowed.HasNone());
    }

    [Fact]
    public async Task PartialSelection_AllowsCorrectPieces()
    {
        // Two files: file 1 spans pieces 0-1, file 2 spans pieces 2-3
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 1000 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 1000, FileSize = 1000 }
        };
        var meta = MakeMetadata(numberOfPieces: 4, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        // Only select file 1
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 1 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        var allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        // File 1: starts at byte 0, size 1000 → pieces 0 (0/500) through 1 ((0+999)/500)
        Assert.True(allowed.HasPiece(0));
        Assert.True(allowed.HasPiece(1));
        // File 2: starts at byte 1000, size 1000 → pieces 2 and 3
        Assert.False(allowed.HasPiece(2));
        Assert.False(allowed.HasPiece(3));
    }

    [Fact]
    public async Task FilesSharePiece_SharedPieceIsAllowed()
    {
        // File 1 ends partway through piece 1; file 2 starts partway through piece 1.
        // If only file 2 is selected, piece 1 should still be allowed (it partially covers file 2).
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 600 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 600, FileSize = 400 }
        };
        // pieceSize = 500 → piece 0 = bytes 0-499, piece 1 = bytes 500-999
        var meta = MakeMetadata(numberOfPieces: 2, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 2 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        var allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        // File 2: starts at byte 600 → firstPiece = 600/500 = 1, lastPiece = (600+400-1)/500 = 1
        Assert.False(allowed.HasPiece(0));
        Assert.True(allowed.HasPiece(1));
    }

    [Fact]
    public async Task Recompute_UpdatesOnSelectionChange()
    {
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 500 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 500, FileSize = 500 }
        };
        var meta = MakeMetadata(numberOfPieces: 2, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 1 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        var allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        Assert.True(allowed.HasPiece(0));
        Assert.False(allowed.HasPiece(1));

        // Change selection to file 2
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 2 });
        await pieceMap.Recompute(TestInfoHash);

        allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        Assert.False(allowed.HasPiece(0));
        Assert.True(allowed.HasPiece(1));
    }

    [Fact]
    public async Task Remove_ClearsFilter()
    {
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "a.txt", FileStartByte = 0, FileSize = 500 },
            new TorrentMetadataFile { Id = 2, Filename = "b.txt", FileStartByte = 500, FileSize = 500 }
        };
        var meta = MakeMetadata(numberOfPieces: 2, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 1 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);
        Assert.NotNull(pieceMap.GetAllowedPieces(TestInfoHash));

        pieceMap.Remove(TestInfoHash);
        Assert.Null(pieceMap.GetAllowedPieces(TestInfoHash));
    }

    [Fact]
    public async Task UnknownTorrent_ReturnsNull()
    {
        var metaCache = new TorrentMetadataCache();
        var fileSelection = new StubFileSelectionService();
        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);

        // Recompute for unknown torrent is a no-op
        await pieceMap.Recompute(TestInfoHash);
        Assert.Null(pieceMap.GetAllowedPieces(TestInfoHash));
    }

    [Fact]
    public async Task LargeFile_SpansManyPieces()
    {
        // Single large file spanning 10 pieces; a second small file in piece 10.
        // Select only the large file → pieces 0-9 allowed, piece 10 not.
        var files = new[]
        {
            new TorrentMetadataFile { Id = 1, Filename = "big.bin", FileStartByte = 0, FileSize = 5000 },
            new TorrentMetadataFile { Id = 2, Filename = "small.txt", FileStartByte = 5000, FileSize = 200 }
        };
        // pieceSize=500, 11 pieces total (5000+200=5200, ceil(5200/500)=11, but we control it)
        var meta = MakeMetadata(numberOfPieces: 11, pieceSize: 500, files);
        var metaCache = new TorrentMetadataCache();
        metaCache.Add(meta);

        var fileSelection = new StubFileSelectionService();
        await fileSelection.SetSelectedFileIds(TestInfoHash, new long[] { 1 });

        var pieceMap = new FileSelectionPieceMap(metaCache, fileSelection);
        await pieceMap.Recompute(TestInfoHash);

        var allowed = pieceMap.GetAllowedPieces(TestInfoHash);
        Assert.NotNull(allowed);
        for (int i = 0; i < 10; i++)
            Assert.True(allowed.HasPiece(i), $"Piece {i} should be allowed");
        Assert.False(allowed.HasPiece(10));
    }
}
