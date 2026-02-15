using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Application;
using Torrential.Application.Persistence;
using Torrential.Core;
using Xunit;

namespace Torrential.Application.Tests;

public class TorrentManagerTests
{
    private static readonly InfoHash TestInfoHash = InfoHash.FromHexString("0123456789ABCDEF0123456789ABCDEF01234567");

    private static TorrentMetaInfo CreateTestMetaInfo(InfoHash? infoHash = null) => new()
    {
        InfoHash = infoHash ?? TestInfoHash,
        Name = "Test Torrent",
        TotalSize = 1024 * 1024,
        PieceSize = 256 * 1024,
        NumberOfPieces = 4,
        Files = new List<TorrentFileInfo>
        {
            new(0, "file1.txt", 512 * 1024),
            new(1, "file2.txt", 256 * 1024),
            new(2, "file3.txt", 256 * 1024)
        },
        AnnounceUrls = new List<string> { "http://tracker.example.com/announce" },
        PieceHashes = new byte[80]
    };

    private static TorrentManager CreateManager()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TorrentDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        return new TorrentManager(NullLogger<TorrentManager>.Instance, scopeFactory);
    }

    // Add Tests

    [Fact]
    public void Add_WithValidMetaInfo_ReturnsSuccess()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();

        var result = manager.Add(metaInfo);

        Assert.True(result.Success);
        var state = manager.GetState(metaInfo.InfoHash);
        Assert.NotNull(state);
        Assert.Equal(TorrentStatus.Added, state.Status);
    }

    [Fact]
    public void Add_WithDuplicateInfoHash_ReturnsTorrentAlreadyExists()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();

        manager.Add(metaInfo);
        var result = manager.Add(metaInfo);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentAlreadyExists, result.Error);
    }

    [Fact]
    public void Add_WithFileSelections_SelectsOnlySpecifiedFiles()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        var selections = new List<TorrentFileSelection>
        {
            new(0, true),
            new(2, true)
        };

        var result = manager.Add(metaInfo, selections);

        Assert.True(result.Success);
        var state = manager.GetState(metaInfo.InfoHash);
        Assert.NotNull(state);
        Assert.Equal(2, state.SelectedFileIndices.Count);
        Assert.Contains(0, state.SelectedFileIndices);
        Assert.Contains(2, state.SelectedFileIndices);
        Assert.DoesNotContain(1, state.SelectedFileIndices);
    }

    [Fact]
    public void Add_WithNullFileSelections_SelectsAllFiles()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();

        var result = manager.Add(metaInfo);

        Assert.True(result.Success);
        var state = manager.GetState(metaInfo.InfoHash);
        Assert.NotNull(state);
        Assert.Equal(3, state.SelectedFileIndices.Count);
        Assert.Contains(0, state.SelectedFileIndices);
        Assert.Contains(1, state.SelectedFileIndices);
        Assert.Contains(2, state.SelectedFileIndices);
    }

    [Fact]
    public void Add_WithInvalidFileIndex_ReturnsInvalidFileSelection()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        var selections = new List<TorrentFileSelection>
        {
            new(0, true),
            new(99, true)
        };

        var result = manager.Add(metaInfo, selections);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.InvalidFileSelection, result.Error);
    }

    // Start Tests

    [Fact]
    public void Start_ExistingTorrent_ReturnsSuccessAndSetsStatusToDownloading()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);

        var result = manager.Start(metaInfo.InfoHash);

        Assert.True(result.Success);
        var state = manager.GetState(metaInfo.InfoHash);
        Assert.NotNull(state);
        Assert.Equal(TorrentStatus.Downloading, state.Status);
    }

    [Fact]
    public void Start_NonExistentTorrent_ReturnsTorrentNotFound()
    {
        var manager = CreateManager();

        var result = manager.Start(TestInfoHash);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentNotFound, result.Error);
    }

    [Fact]
    public void Start_AlreadyRunningTorrent_ReturnsTorrentAlreadyRunning()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);
        manager.Start(metaInfo.InfoHash);

        var result = manager.Start(metaInfo.InfoHash);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentAlreadyRunning, result.Error);
    }

    // Stop Tests

    [Fact]
    public void Stop_RunningTorrent_ReturnsSuccessAndSetsStatusToStopped()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);
        manager.Start(metaInfo.InfoHash);

        var result = manager.Stop(metaInfo.InfoHash);

        Assert.True(result.Success);
        var state = manager.GetState(metaInfo.InfoHash);
        Assert.NotNull(state);
        Assert.Equal(TorrentStatus.Stopped, state.Status);
    }

    [Fact]
    public void Stop_NonExistentTorrent_ReturnsTorrentNotFound()
    {
        var manager = CreateManager();

        var result = manager.Stop(TestInfoHash);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentNotFound, result.Error);
    }

    [Fact]
    public void Stop_AlreadyStoppedTorrent_ReturnsTorrentAlreadyStopped()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);

        var result = manager.Stop(metaInfo.InfoHash);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentAlreadyStopped, result.Error);
    }

    // Remove Tests

    [Fact]
    public void Remove_ExistingTorrent_ReturnsSuccessAndRemovesFromState()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);

        var result = manager.Remove(metaInfo.InfoHash);

        Assert.True(result.Success);
        Assert.Null(manager.GetState(metaInfo.InfoHash));
    }

    [Fact]
    public void Remove_NonExistentTorrent_ReturnsTorrentNotFound()
    {
        var manager = CreateManager();

        var result = manager.Remove(TestInfoHash);

        Assert.False(result.Success);
        Assert.Equal(TorrentManagerError.TorrentNotFound, result.Error);
    }

    [Fact]
    public void Remove_WithDeleteData_ReturnsSuccess()
    {
        var manager = CreateManager();
        var metaInfo = CreateTestMetaInfo();
        manager.Add(metaInfo);

        var result = manager.Remove(metaInfo.InfoHash, deleteData: true);

        Assert.True(result.Success);
        Assert.Null(manager.GetState(metaInfo.InfoHash));
    }

    // GetAll Tests

    [Fact]
    public void GetAll_ReturnsAllManagedTorrents()
    {
        var manager = CreateManager();
        var metaInfo1 = CreateTestMetaInfo();
        var metaInfo2 = CreateTestMetaInfo(InfoHash.FromHexString("ABCDEF0123456789ABCDEF0123456789ABCDEF01"));

        manager.Add(metaInfo1);
        manager.Add(metaInfo2);

        var all = manager.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_EmptyManager_ReturnsEmptyList()
    {
        var manager = CreateManager();

        var all = manager.GetAll();

        Assert.Empty(all);
    }
}
