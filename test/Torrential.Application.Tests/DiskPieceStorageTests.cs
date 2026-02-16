using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Application;
using Torrential.Application.Persistence;
using Torrential.Core;
using Xunit;

namespace Torrential.Application.Tests;

public class DiskPieceStorageTests : IDisposable
{
    private static readonly InfoHash TestInfoHash = InfoHash.FromHexString("0123456789ABCDEF0123456789ABCDEF01234567");
    private readonly string _tempDir;
    private readonly DiskPieceStorage _storage;

    public DiskPieceStorageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "torrential-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var settingsService = new StubSettingsService(_tempDir);
        _storage = new DiskPieceStorage(settingsService, NullLogger<DiskPieceStorage>.Instance);
    }

    public void Dispose()
    {
        _storage.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static TorrentMetaInfo CreateMetaInfo(
        string name = "Test Torrent",
        long pieceSize = 256 * 1024,
        List<TorrentFileInfo>? files = null)
    {
        files ??= [new(0, "file1.txt", 512 * 1024), new(1, "file2.txt", 256 * 1024)];
        var totalSize = files.Sum(f => f.FileSize);
        var numberOfPieces = (int)((totalSize + pieceSize - 1) / pieceSize);
        return new TorrentMetaInfo
        {
            InfoHash = TestInfoHash,
            Name = name,
            TotalSize = totalSize,
            PieceSize = pieceSize,
            NumberOfPieces = numberOfPieces,
            Files = files,
            AnnounceUrls = ["http://tracker.example.com/announce"],
            PieceHashes = new byte[numberOfPieces * 20]
        };
    }

    // InitializeTorrentStorageAsync tests

    [Fact]
    public async Task Initialize_CreatesTorrentFolderWithSanitizedName()
    {
        var metaInfo = CreateMetaInfo(name: "My:Torrent<>Name");

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        var expectedFolder = Path.Combine(_tempDir, "My_Torrent__Name");
        Assert.True(Directory.Exists(expectedFolder));
    }

    [Fact]
    public async Task Initialize_PreAllocatesPartFilesAtCorrectSizes()
    {
        var files = new List<TorrentFileInfo>
        {
            new(0, "file1.txt", 100_000),
            new(1, "file2.txt", 200_000)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 65536);

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        var torrentFolder = Path.Combine(_tempDir, "Test Torrent");
        var part1 = Path.Combine(torrentFolder, "file1.txt.part");
        var part2 = Path.Combine(torrentFolder, "file2.txt.part");

        Assert.True(File.Exists(part1));
        Assert.True(File.Exists(part2));
        Assert.Equal(100_000, new FileInfo(part1).Length);
        Assert.Equal(200_000, new FileInfo(part2).Length);
    }

    [Fact]
    public async Task Initialize_CreatesSubdirectoriesForNestedFilePaths()
    {
        var files = new List<TorrentFileInfo>
        {
            new(0, "subdir/nested/file.txt", 1024)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 1024);

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        var torrentFolder = Path.Combine(_tempDir, "Test Torrent");
        var partPath = Path.Combine(torrentFolder, "subdir", "nested", "file.txt.part");

        Assert.True(Directory.Exists(Path.Combine(torrentFolder, "subdir", "nested")));
        Assert.True(File.Exists(partPath));
        Assert.Equal(1024, new FileInfo(partPath).Length);
    }

    [Fact]
    public async Task Initialize_SkipsReCreationIfPartFileAlreadyExistsAtCorrectSize()
    {
        var files = new List<TorrentFileInfo> { new(0, "file.txt", 1024) };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 1024);

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        var torrentFolder = Path.Combine(_tempDir, "Test Torrent");
        var partPath = Path.Combine(torrentFolder, "file.txt.part");
        var firstWriteTime = File.GetLastWriteTimeUtc(partPath);

        // Small delay to ensure timestamp would differ
        await Task.Delay(50);

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        // File should not have been recreated
        var secondWriteTime = File.GetLastWriteTimeUtc(partPath);
        Assert.Equal(firstWriteTime, secondWriteTime);
        Assert.Equal(1024, new FileInfo(partPath).Length);
    }

    // IsFileComplete tests

    [Fact]
    public void IsFileComplete_ReturnsFalseWhenNotAllPiecesDownloaded()
    {
        // 2 files: file1 = 512KB (pieces 0-1), file2 = 256KB (piece 2)
        // pieceSize = 256KB, so 3 pieces total
        var files = new List<TorrentFileInfo>
        {
            new(0, "file1.txt", 512 * 1024),
            new(1, "file2.txt", 256 * 1024)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 256 * 1024);

        using var bitfield = new Bitfield(metaInfo.NumberOfPieces);
        bitfield.MarkHave(0); // Only have piece 0, need 0 and 1 for file1

        Assert.False(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));
    }

    [Fact]
    public void IsFileComplete_ReturnsTrueWhenAllPiecesDownloaded()
    {
        var files = new List<TorrentFileInfo>
        {
            new(0, "file1.txt", 512 * 1024),
            new(1, "file2.txt", 256 * 1024)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 256 * 1024);

        using var bitfield = new Bitfield(metaInfo.NumberOfPieces);
        bitfield.MarkHave(0);
        bitfield.MarkHave(1);

        Assert.True(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));
    }

    [Fact]
    public void IsFileComplete_HandlesFilesSpanningPartialPieces()
    {
        // pieceSize = 100, file1 = 150 bytes (pieces 0-1), file2 = 50 bytes (piece 1)
        // Piece 1 is shared between file1 and file2
        var files = new List<TorrentFileInfo>
        {
            new(0, "file1.txt", 150),
            new(1, "file2.txt", 50)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 100);
        // totalSize = 200, pieces = 2 (piece 0: 0-99, piece 1: 100-199)

        using var bitfield = new Bitfield(metaInfo.NumberOfPieces);
        bitfield.MarkHave(0);
        // file1 spans pieces 0-1, missing piece 1
        Assert.False(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));

        bitfield.MarkHave(1);
        // Now both pieces for file1 are present
        Assert.True(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));
        // file2 occupies piece 1, which is also present
        Assert.True(_storage.IsFileComplete(TestInfoHash, 1, metaInfo, bitfield));
    }

    // FinalizeFileAsync tests

    [Fact]
    public async Task FinalizeFile_RenamesPartFileToActualName()
    {
        var files = new List<TorrentFileInfo> { new(0, "file.txt", 1024) };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 1024);

        await _storage.InitializeTorrentStorageAsync(metaInfo);

        var torrentFolder = Path.Combine(_tempDir, "Test Torrent");
        var partPath = Path.Combine(torrentFolder, "file.txt.part");
        var finalPath = Path.Combine(torrentFolder, "file.txt");

        Assert.True(File.Exists(partPath));
        Assert.False(File.Exists(finalPath));

        await _storage.FinalizeFileAsync(TestInfoHash, 0, metaInfo);

        Assert.False(File.Exists(partPath));
        Assert.True(File.Exists(finalPath));
    }

    [Fact]
    public async Task FinalizeFile_IsIdempotent()
    {
        var files = new List<TorrentFileInfo> { new(0, "file.txt", 1024) };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 1024);

        await _storage.InitializeTorrentStorageAsync(metaInfo);
        await _storage.FinalizeFileAsync(TestInfoHash, 0, metaInfo);

        // Second call should not throw
        await _storage.FinalizeFileAsync(TestInfoHash, 0, metaInfo);

        var torrentFolder = Path.Combine(_tempDir, "Test Torrent");
        Assert.True(File.Exists(Path.Combine(torrentFolder, "file.txt")));
    }

    // Piece-to-file mapping test

    [Fact]
    public void IsFileComplete_CorrectlyDeterminesWhichPiecesBelongToFile()
    {
        // 3 files with known sizes, pieceSize = 100
        // file0: 250 bytes -> offset 0-249 -> pieces 0,1,2
        // file1: 100 bytes -> offset 250-349 -> pieces 2,3
        // file2: 50 bytes  -> offset 350-399 -> piece 3
        var files = new List<TorrentFileInfo>
        {
            new(0, "a.bin", 250),
            new(1, "b.bin", 100),
            new(2, "c.bin", 50)
        };
        var metaInfo = CreateMetaInfo(files: files, pieceSize: 100);
        // totalSize=400, pieces=4

        using var bitfield = new Bitfield(metaInfo.NumberOfPieces);

        // file0 needs pieces 0,1,2
        bitfield.MarkHave(0);
        bitfield.MarkHave(1);
        Assert.False(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));
        bitfield.MarkHave(2);
        Assert.True(_storage.IsFileComplete(TestInfoHash, 0, metaInfo, bitfield));

        // file1 needs pieces 2,3
        Assert.False(_storage.IsFileComplete(TestInfoHash, 1, metaInfo, bitfield));
        bitfield.MarkHave(3);
        Assert.True(_storage.IsFileComplete(TestInfoHash, 1, metaInfo, bitfield));

        // file2 needs piece 3 (already have it)
        Assert.True(_storage.IsFileComplete(TestInfoHash, 2, metaInfo, bitfield));
    }

    private sealed class StubSettingsService(string downloadFolder) : ISettingsService
    {
        public Task<SettingsEntity> GetSettingsAsync() =>
            Task.FromResult(new SettingsEntity { DownloadFolder = downloadFolder });

        public Task<SettingsEntity> UpdateSettingsAsync(string downloadFolder1, string completedFolder, int maxHalfOpenConnections, int maxPeersPerTorrent) =>
            throw new NotImplementedException();
    }
}
