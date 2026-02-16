using Torrential.Files;

namespace Torrential.Tests;

/// <summary>
/// Tests for IFileSelectionService contract behavior using a minimal in-memory implementation.
/// The production FileSelectionService wraps a ConcurrentDictionary cache with disk persistence;
/// these tests verify the logical contracts (get/set/initialize idempotency) that the rest of
/// the system depends on.
/// </summary>
public class FileSelectionServiceTests
{
    /// <summary>
    /// In-memory IFileSelectionService that mirrors the cache-layer behavior of
    /// FileSelectionService without requiring SettingsManager or disk I/O.
    /// </summary>
    private sealed class InMemoryFileSelectionService : IFileSelectionService
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

        public void InitializeAllSelected(InfoHash infoHash, IEnumerable<long> allFileIds)
        {
            var key = infoHash.AsString();
            if (!_store.ContainsKey(key))
                _store[key] = new HashSet<long>(allFileIds);
        }
    }

    private static InfoHash TestInfoHash => InfoHash.FromHexString("0102030405060708091011121314151617181920");
    private static InfoHash OtherInfoHash => InfoHash.FromHexString("A1A2A3A4A5A6A7A8A9A0B1B2B3B4B5B6B7B8B9B0");

    [Fact]
    public async Task GetSelectedFileIds_UnknownTorrent_ReturnsEmpty()
    {
        var service = new InMemoryFileSelectionService();
        var result = await service.GetSelectedFileIds(TestInfoHash);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SetSelectedFileIds_StoresValues()
    {
        var service = new InMemoryFileSelectionService();
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 1, 2, 3 });

        var result = await service.GetSelectedFileIds(TestInfoHash);
        Assert.Equal(3, result.Count);
        Assert.Contains(1L, result);
        Assert.Contains(2L, result);
        Assert.Contains(3L, result);
    }

    [Fact]
    public async Task SetSelectedFileIds_OverwritesPreviousSelection()
    {
        var service = new InMemoryFileSelectionService();
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 1, 2, 3 });
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 2 });

        var result = await service.GetSelectedFileIds(TestInfoHash);
        Assert.Single(result);
        Assert.Contains(2L, result);
    }

    [Fact]
    public async Task SetSelectedFileIds_EmptyArray_ResultsInEmptySet()
    {
        var service = new InMemoryFileSelectionService();
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 1, 2 });
        await service.SetSelectedFileIds(TestInfoHash, Array.Empty<long>());

        var result = await service.GetSelectedFileIds(TestInfoHash);
        Assert.Empty(result);
    }

    [Fact]
    public void InitializeAllSelected_PopulatesForNewTorrent()
    {
        var service = new InMemoryFileSelectionService();
        service.InitializeAllSelected(TestInfoHash, new long[] { 1, 2, 3 });

        var result = service.GetSelectedFileIds(TestInfoHash).Result;
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void InitializeAllSelected_DoesNotOverwriteExisting()
    {
        var service = new InMemoryFileSelectionService();
        service.InitializeAllSelected(TestInfoHash, new long[] { 1, 2, 3 });
        service.InitializeAllSelected(TestInfoHash, new long[] { 4, 5, 6 });

        var result = service.GetSelectedFileIds(TestInfoHash).Result;
        Assert.Equal(3, result.Count);
        Assert.Contains(1L, result);
        Assert.DoesNotContain(4L, result);
    }

    [Fact]
    public async Task DifferentTorrents_IndependentSelections()
    {
        var service = new InMemoryFileSelectionService();
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 1, 2 });
        await service.SetSelectedFileIds(OtherInfoHash, new long[] { 5, 6, 7 });

        var result1 = await service.GetSelectedFileIds(TestInfoHash);
        var result2 = await service.GetSelectedFileIds(OtherInfoHash);

        Assert.Equal(2, result1.Count);
        Assert.Equal(3, result2.Count);
        Assert.Contains(1L, result1);
        Assert.Contains(5L, result2);
    }

    [Fact]
    public async Task DuplicateFileIds_DeduplicatedInSet()
    {
        var service = new InMemoryFileSelectionService();
        await service.SetSelectedFileIds(TestInfoHash, new long[] { 1, 1, 2, 2 });

        var result = await service.GetSelectedFileIds(TestInfoHash);
        Assert.Equal(2, result.Count);
    }
}
