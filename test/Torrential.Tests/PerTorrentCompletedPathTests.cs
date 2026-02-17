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

public sealed class PerTorrentCompletedPathTests
{
    [Fact]
    public async Task Completed_path_uses_torrent_specific_override_when_present()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-pertorrent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? serviceProvider = null;

        try
        {
            var (sp, settingsManager) = await CreateInfrastructure(tempRoot);
            serviceProvider = sp;

            var customCompletedPath = Path.Combine(tempRoot, "custom-completed");
            var infoHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
                db.Torrents.Add(new TorrentConfiguration
                {
                    InfoHash = infoHash,
                    DownloadPath = "",
                    CompletedPath = customCompletedPath,
                    Status = TorrentStatus.Running,
                    DateAdded = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var metadata = CreateMetadata("custom-path-test", infoHash);
            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager, serviceProvider.GetRequiredService<IServiceScopeFactory>());
            var completedPath = await fileService.GetCompletedTorrentPath(metadata.InfoHash);

            Assert.StartsWith(customCompletedPath, completedPath);
        }
        finally
        {
            serviceProvider?.Dispose();
            TryCleanup(tempRoot);
        }
    }

    [Fact]
    public async Task Completed_path_falls_back_to_global_settings_when_no_torrent_row_exists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-pertorrent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? serviceProvider = null;

        try
        {
            var (sp, settingsManager) = await CreateInfrastructure(tempRoot);
            serviceProvider = sp;

            var globalCompletedPath = Path.Combine(tempRoot, "completed");
            var infoHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

            var metadata = CreateMetadata("fallback-test", infoHash);
            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager, serviceProvider.GetRequiredService<IServiceScopeFactory>());
            var completedPath = await fileService.GetCompletedTorrentPath(metadata.InfoHash);

            Assert.StartsWith(globalCompletedPath, completedPath);
        }
        finally
        {
            serviceProvider?.Dispose();
            TryCleanup(tempRoot);
        }
    }

    [Fact]
    public async Task Completed_files_land_under_custom_completed_root()
    {
        await using var harness = await FileCopyTestHarness.CreateWithCustomCompletedPathAsync();

        var metadata = CreateMetadata(
            name: "custom-root-copy",
            infoHash: harness.InfoHash,
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "data/readme.txt", FileStartByte = 0, FileSize = 13, IsSelected = true }
            ]);

        harness.MetadataCache.Add(metadata);
        await harness.WritePartFileAsync(metadata.InfoHash, "hello custom!"u8.ToArray());

        var result = await harness.Action.ExecuteAsync(metadata.InfoHash, CancellationToken.None);
        Assert.True(result.Success);

        var completedTorrentPath = await harness.FileHandleProvider.GetCompletedTorrentPath(metadata.InfoHash);
        Assert.StartsWith(harness.CustomCompletedPath, completedTorrentPath);

        var outputPath = Path.Combine(completedTorrentPath, "data", "readme.txt");
        Assert.True(File.Exists(outputPath));
        Assert.Equal("hello custom!"u8.ToArray(), await File.ReadAllBytesAsync(outputPath));
    }

    private static TorrentMetadata CreateMetadata(string name, string infoHash, TorrentMetadataFile[]? files = null)
    {
        files ??=
        [
            new TorrentMetadataFile { Id = 0, Filename = "file.txt", FileStartByte = 0, FileSize = 10, IsSelected = true }
        ];

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

    private static async Task<(ServiceProvider serviceProvider, SettingsManager settingsManager)> CreateInfrastructure(string tempRoot)
    {
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

        return (serviceProvider, settingsManager);
    }

    private static void TryCleanup(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup on Windows when file handles are slow to release.
        }
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
            string customCompletedPath,
            string infoHash,
            TorrentMetadataCache metadataCache,
            TorrentFileService torrentFileService,
            IFileHandleProvider fileHandleProvider,
            TorrentEventBus eventBus,
            FileCopyPostDownloadAction action,
            object internalFileHandleProvider)
        {
            _serviceProvider = serviceProvider;
            _tempRoot = tempRoot;
            _internalFileHandleProvider = internalFileHandleProvider;
            CustomCompletedPath = customCompletedPath;
            InfoHash = infoHash;
            MetadataCache = metadataCache;
            TorrentFileService = torrentFileService;
            FileHandleProvider = fileHandleProvider;
            EventBus = eventBus;
            Action = action;
        }

        public string CustomCompletedPath { get; }
        public string InfoHash { get; }
        public TorrentMetadataCache MetadataCache { get; }
        public TorrentFileService TorrentFileService { get; }
        public IFileHandleProvider FileHandleProvider { get; }
        public TorrentEventBus EventBus { get; }
        public FileCopyPostDownloadAction Action { get; }

        public static async Task<FileCopyTestHarness> CreateWithCustomCompletedPathAsync()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-pertorrent-copy-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);

            var customCompletedPath = Path.Combine(tempRoot, "custom-completed");
            var infoHash = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";

            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddLogging();
            services.AddDbContext<TorrentialDb>(options => options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));

            var serviceProvider = services.BuildServiceProvider();
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TorrentialDb>();
                await db.Database.EnsureCreatedAsync();

                db.Torrents.Add(new TorrentConfiguration
                {
                    InfoHash = infoHash,
                    DownloadPath = "",
                    CompletedPath = customCompletedPath,
                    Status = TorrentStatus.Running,
                    DateAdded = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
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
            var torrentFileService = new TorrentFileService(metadataCache, settingsManager, serviceProvider.GetRequiredService<IServiceScopeFactory>());

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
                customCompletedPath,
                infoHash,
                metadataCache,
                torrentFileService,
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
