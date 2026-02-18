using System.IO.Compression;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Files;
using Torrential.Pipelines;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Tests;

public sealed class FileCopyPostDownloadActionTests
{
    [Fact]
    public async Task Non_archive_selected_file_copies_unchanged_from_part_to_completed_path()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();
        var metadata = CreateMetadata(
            name: "copy-raw",
            infoHash: "1111111111111111111111111111111111111111",
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "padding.bin", FileStartByte = 0, FileSize = 5, IsSelected = false },
                new TorrentMetadataFile { Id = 1, Filename = "media/video.txt", FileStartByte = 5, FileSize = 12, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);

        var partPayload = CombineSegments(
            "12345"u8.ToArray(),
            "hello world!"u8.ToArray());

        await harness.WritePartFileAsync(metadata.InfoHash, partPayload);
        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);

        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        var outputPath = Path.Combine(completedTorrentPath, "media", "video.txt");
        Assert.True(File.Exists(outputPath));
        Assert.Equal("hello world!"u8.ToArray(), await File.ReadAllBytesAsync(outputPath));
    }

    [Fact]
    public async Task Archive_selected_file_extracts_directly_without_creating_intermediate_archive()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();
        var archiveBytes = CreateZipArchive([("album/track.txt", "line-1\nline-2"u8.ToArray())]);
        var metadata = CreateMetadata(
            name: "extract-direct",
            infoHash: "2222222222222222222222222222222222222222",
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "seed.bin", FileStartByte = 0, FileSize = 4, IsSelected = false },
                new TorrentMetadataFile { Id = 1, Filename = "release/archive.zip", FileStartByte = 4, FileSize = archiveBytes.Length, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, CombineSegments("seed"u8.ToArray(), archiveBytes));

        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);
        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        var extractedFilePath = Path.Combine(completedTorrentPath, "release", "album", "track.txt");
        var rawArchivePath = Path.Combine(completedTorrentPath, "release", "archive.zip");

        Assert.True(File.Exists(extractedFilePath));
        Assert.False(File.Exists(rawArchivePath));
        Assert.Equal("line-1\nline-2"u8.ToArray(), await File.ReadAllBytesAsync(extractedFilePath));
    }

    [Fact]
    public async Task Multipart_rar_extracts_from_primary_volume_and_skips_raw_volume_copy()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();

        var volumeBytes = Enumerable.Range(1, 6)
            .Select(index => ReadRarFixtureBytes($"Rar.multi.part0{index}.rar"))
            .ToArray();

        var files = new List<TorrentMetadataFile>();
        var start = 0L;
        for (var index = 0; index < volumeBytes.Length; index++)
        {
            var payload = volumeBytes[index];
            files.Add(new TorrentMetadataFile
            {
                Id = index,
                Filename = $"release/Rar.multi.part0{index + 1}.rar",
                FileStartByte = start,
                FileSize = payload.Length,
                IsSelected = true
            });
            start += payload.Length;
        }

        var metadata = CreateMetadata(
            name: "extract-rar-multipart",
            infoHash: "5555555555555555555555555555555555555555",
            files: files.ToArray());

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, CombineSegments(volumeBytes));

        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);
        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        Assert.True(File.Exists(Path.Combine(completedTorrentPath, "release", "exe", "test.exe")));
        Assert.True(File.Exists(Path.Combine(completedTorrentPath, "release", "jpg", "test.jpg")));

        for (var index = 1; index <= volumeBytes.Length; index++)
            Assert.False(File.Exists(Path.Combine(completedTorrentPath, "release", $"Rar.multi.part0{index}.rar")));
    }

    [Fact]
    public async Task Legacy_multipart_rar_extracts_from_rar_and_skips_r00_r01_volumes()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();

        var volumeBytes = Enumerable.Range(1, 6)
            .Select(index => ReadRarFixtureBytes($"Rar.multi.part0{index}.rar"))
            .ToArray();

        var volumeNames = new[] { "Rar.multi.rar", "Rar.multi.r00", "Rar.multi.r01", "Rar.multi.r02", "Rar.multi.r03", "Rar.multi.r04" };
        var files = new List<TorrentMetadataFile>();
        var start = 0L;
        for (var index = 0; index < volumeBytes.Length; index++)
        {
            var payload = volumeBytes[index];
            files.Add(new TorrentMetadataFile
            {
                Id = index,
                Filename = $"release/{volumeNames[index]}",
                FileStartByte = start,
                FileSize = payload.Length,
                IsSelected = true
            });
            start += payload.Length;
        }

        var metadata = CreateMetadata(
            name: "extract-rar-legacy-multipart",
            infoHash: "6666666666666666666666666666666666666666",
            files: files.ToArray());

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, CombineSegments(volumeBytes));

        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);
        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        Assert.True(File.Exists(Path.Combine(completedTorrentPath, "release", "exe", "test.exe")));
        Assert.True(File.Exists(Path.Combine(completedTorrentPath, "release", "jpg", "test.jpg")));

        foreach (var volumeName in volumeNames)
            Assert.False(File.Exists(Path.Combine(completedTorrentPath, "release", volumeName)));
    }

    [Fact]
    public async Task Corrupt_archive_falls_back_to_raw_copy()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();
        var corruptArchiveBytes = "this is not a valid zip payload"u8.ToArray();
        var metadata = CreateMetadata(
            name: "extract-fallback",
            infoHash: "3333333333333333333333333333333333333333",
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "bad/corrupt.zip", FileStartByte = 0, FileSize = corruptArchiveBytes.Length, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, corruptArchiveBytes);

        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);
        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        var rawArchivePath = Path.Combine(completedTorrentPath, "bad", "corrupt.zip");
        var extractedFilePath = Path.Combine(completedTorrentPath, "bad", "corrupt");

        Assert.True(File.Exists(rawArchivePath));
        Assert.Equal(corruptArchiveBytes, await File.ReadAllBytesAsync(rawArchivePath));
        Assert.False(File.Exists(extractedFilePath));
    }

    [Fact]
    public async Task Cancellation_before_materialization_leaves_no_completed_output()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();
        var payload = "cancel-me"u8.ToArray();
        var metadata = CreateMetadata(
            name: "cancel-copy",
            infoHash: "4444444444444444444444444444444444444444",
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "docs/cancel.txt", FileStartByte = 0, FileSize = payload.Length, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, payload);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, cts.Token);

        Assert.False(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        var outputPath = Path.Combine(completedTorrentPath, "docs", "cancel.txt");
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task Torrent_configuration_completed_path_is_used_for_materialization()
    {
        await using var harness = await FileCopyTestHarness.CreateAsync();
        var metadata = CreateMetadata(
            name: "copy-custom-completed",
            infoHash: "7777777777777777777777777777777777777777",
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "payload/custom.txt", FileStartByte = 0, FileSize = 11, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);
        var customCompletedRoot = Path.Combine(harness.TempRoot, "completed-custom");
        await harness.UpsertTorrentConfigurationAsync(metadata.InfoHash, harness.DefaultDownloadRoot, customCompletedRoot);

        await harness.WritePartFileAsync(metadata.InfoHash, "custom-path"u8.ToArray());
        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);

        Assert.True(result.Success);

        var expectedOutput = Path.Combine(customCompletedRoot, metadata.Name, "payload", "custom.txt");
        var defaultOutput = Path.Combine(harness.DefaultCompletedRoot, metadata.Name, "payload", "custom.txt");

        Assert.True(File.Exists(expectedOutput));
        Assert.Equal("custom-path"u8.ToArray(), await File.ReadAllBytesAsync(expectedOutput));
        Assert.False(File.Exists(defaultOutput));
    }

    private static TorrentMetadata CreateMetadata(string name, string infoHash, TorrentMetadataFile[] files)
    {
        var totalSize = files.Sum(static file => file.FileSize);
        return new TorrentMetadata
        {
            Name = name,
            UrlList = Array.Empty<string>(),
            AnnounceList = Array.Empty<string>(),
            Files = files,
            PieceSize = 16 * 1024,
            TotalSize = totalSize,
            InfoHash = infoHash,
            PieceHashesConcatenated = new byte[20]
        };
    }

    private static byte[] CombineSegments(params byte[][] segments)
    {
        var totalLength = segments.Sum(static segment => segment.Length);
        var output = new byte[totalLength];
        var offset = 0;
        foreach (var segment in segments)
        {
            Buffer.BlockCopy(segment, 0, output, offset, segment.Length);
            offset += segment.Length;
        }

        return output;
    }

    private static byte[] CreateZipArchive((string Path, byte[] Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var archiveEntry = archive.CreateEntry(entry.Path, CompressionLevel.NoCompression);
                using var entryStream = archiveEntry.Open();
                entryStream.Write(entry.Content, 0, entry.Content.Length);
            }
        }

        return stream.ToArray();
    }

    private static byte[] ReadRarFixtureBytes(string fileName)
    {
        var root = ResolveSolutionRoot();
        var fixturePath = Path.Combine(root, "test", "Torrential.Tests", "Fixtures", "RarMulti", fileName);
        return File.ReadAllBytes(fixturePath);
    }

    private static string ResolveSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Torrential.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate solution root from test base directory.");
    }

    private sealed class FileCopyTestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly string _tempRoot;
        private readonly object _internalFileHandleProvider;
        private bool _disposed;

        private FileCopyTestHarness(
            ServiceProvider serviceProvider,
            string tempRoot,
            TorrentMetadataCache metadataCache,
            TorrentFileService torrentFileService,
            SettingsManager settingsManager,
            IFileHandleProvider fileHandleProvider,
            TorrentEventBus eventBus,
            FileCopyPostDownloadAction action,
            object internalFileHandleProvider)
        {
            _serviceProvider = serviceProvider;
            _tempRoot = tempRoot;
            _internalFileHandleProvider = internalFileHandleProvider;
            MetadataCache = metadataCache;
            TorrentFileService = torrentFileService;
            SettingsManager = settingsManager;
            FileHandleProvider = fileHandleProvider;
            EventBus = eventBus;
            Action = action;
        }

        public TorrentMetadataCache MetadataCache { get; }
        public TorrentFileService TorrentFileService { get; }
        public SettingsManager SettingsManager { get; }
        public IFileHandleProvider FileHandleProvider { get; }
        public TorrentEventBus EventBus { get; }
        public FileCopyPostDownloadAction Action { get; }
        public string TempRoot => _tempRoot;
        public string DefaultDownloadRoot => Path.Combine(_tempRoot, "download");
        public string DefaultCompletedRoot => Path.Combine(_tempRoot, "completed");

        public static async Task<FileCopyTestHarness> CreateAsync()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-postcopy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging();
            services.AddDbContext<TorrentialDb>(options => options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));

            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
                await db.Database.EnsureCreatedAsync();
            }

            var settingsManager = new SettingsManager(
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                serviceProvider.GetRequiredService<IMemoryCache>());

            await settingsManager.SaveFileSettings(new FileSettings
            {
                DownloadPath = Path.Combine(tempRoot, "download"),
                CompletedPath = Path.Combine(tempRoot, "completed")
            });

            var metadataCache = new TorrentMetadataCache();
            var torrentFileService = new TorrentFileService(
                metadataCache,
                settingsManager,
                serviceProvider.GetRequiredService<IServiceScopeFactory>());
            var fileHandleProviderType = typeof(TorrentFileService).Assembly.GetType("Torrential.Files.FileHandleProvider", throwOnError: true)!;
            var internalFileHandleProvider = Activator.CreateInstance(fileHandleProviderType, metadataCache, torrentFileService)
                ?? throw new InvalidOperationException("Failed to create FileHandleProvider.");
            var fileHandleProvider = (IFileHandleProvider)internalFileHandleProvider;

            var archiveExtractionServiceType = typeof(TorrentFileService).Assembly.GetType("Torrential.Files.ArchiveExtractionService", throwOnError: true)!;
            var archiveLoggerType = typeof(Microsoft.Extensions.Logging.ILogger<>).MakeGenericType(archiveExtractionServiceType);
            var archiveLogger = serviceProvider.GetRequiredService(archiveLoggerType);
            var archiveExtractionService = (IArchiveExtractionService)(Activator.CreateInstance(
                archiveExtractionServiceType,
                archiveLogger,
                fileHandleProvider,
                metadataCache) ?? throw new InvalidOperationException("Failed to create ArchiveExtractionService."));

            var eventBus = new TorrentEventBus();
            var action = new FileCopyPostDownloadAction(
                metadataCache,
                fileHandleProvider,
                archiveExtractionService,
                eventBus,
                NullLogger<FileCopyPostDownloadAction>.Instance);

            return new FileCopyTestHarness(
                serviceProvider,
                tempRoot,
                metadataCache,
                torrentFileService,
                settingsManager,
                fileHandleProvider,
                eventBus,
                action,
                internalFileHandleProvider);
        }

        public async Task WritePartFileAsync(InfoHash infoHash, byte[] content)
        {
            var partPath = await TorrentFileService.GetPartFilePath(infoHash);
            Directory.CreateDirectory(Path.GetDirectoryName(partPath)!);
            await File.WriteAllBytesAsync(partPath, content);
        }

        public async Task UpsertTorrentConfigurationAsync(InfoHash infoHash, string downloadPath, string completedPath)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
            var config = await db.Torrents.FirstOrDefaultAsync(x => x.InfoHash == infoHash.AsString());

            if (config == null)
            {
                config = new TorrentConfiguration
                {
                    InfoHash = infoHash,
                    DownloadPath = downloadPath,
                    CompletedPath = completedPath,
                    DateAdded = DateTimeOffset.UtcNow,
                    Status = TorrentStatus.Idle
                };

                await db.Torrents.AddAsync(config);
            }
            else
            {
                config.DownloadPath = downloadPath;
                config.CompletedPath = completedPath;
            }

            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await EventBus.DisposeAsync();
            CloseCachedPartHandles();
            _serviceProvider.Dispose();

            if (!Directory.Exists(_tempRoot))
                return;

            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup on Windows when file handles are slow to release.
            }
        }

        private void CloseCachedPartHandles()
        {
            var field = _internalFileHandleProvider
                .GetType()
                .GetField("_partFiles", BindingFlags.Instance | BindingFlags.Public);

            if (field?.GetValue(_internalFileHandleProvider) is not System.Collections.IEnumerable entries)
                return;

            foreach (var entry in entries)
            {
                var value = entry?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(entry);
                if (value is Microsoft.Win32.SafeHandles.SafeFileHandle handle)
                {
                    handle.Close();
                }
            }
        }
    }
}
