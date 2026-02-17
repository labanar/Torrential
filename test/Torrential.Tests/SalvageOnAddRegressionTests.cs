using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Tests;

public class SalvageOnAddRegressionTests
{
    /// <summary>
    /// When a part file with data already exists on disk but no persisted bitfields are present,
    /// the BitfieldManager should mark candidate pieces as downloaded so they get queued
    /// for SHA-1 validation through the event bus channel.
    /// </summary>
    [Fact]
    public async Task Existing_part_file_without_bitfields_marks_candidate_pieces_for_validation()
    {
        var tempRoot = CreateTempRoot();
        ServiceProvider? serviceProvider = null;

        try
        {
            const int pieceSize = 32;
            const int numPieces = 4;
            const long totalSize = pieceSize * numPieces; // 128 bytes, 4 full pieces

            var metadata = CreateMetadata(pieceSize, totalSize, numPieces);
            var (settingsManager, sp) = await BuildSettingsManager(tempRoot);
            serviceProvider = sp;

            var downloadDir = SetupDownloadDirectory(tempRoot, metadata);

            // Create a part file that covers the first 3 pieces (96 bytes out of 128)
            var partFilePath = Path.Combine(downloadDir, $"{metadata.InfoHash.AsString()}.part");
            var partFileData = new byte[pieceSize * 3];
            new Random(42).NextBytes(partFileData);
            await File.WriteAllBytesAsync(partFilePath, partFileData);

            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager);
            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);

            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);

            var recoveryResult = new RecoverableDataResult
            {
                HasRecoverableData = true,
                PartFileLength = partFileData.Length,
                Reason = "Part file found"
            };

            await bitfieldManager.Initialize(metadata, recoveryResult);

            // The download bitfield should have the 3 candidate pieces marked
            // (covered by 96 bytes / 32-byte pieces = 3 pieces)
            Assert.True(bitfieldManager.TryGetDownloadBitfield(metadata.InfoHash, out var downloadBf));
            Assert.True(downloadBf.HasPiece(0));
            Assert.True(downloadBf.HasPiece(1));
            Assert.True(downloadBf.HasPiece(2));
            Assert.False(downloadBf.HasPiece(3));

            // Verification bitfield should still be empty (no SHA-1 check has run)
            Assert.True(bitfieldManager.TryGetVerificationBitfield(metadata.InfoHash, out var verificationBf));
            Assert.True(verificationBf.HasNone());
        }
        finally
        {
            serviceProvider?.Dispose();
            CleanupTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// When a torrent is newly added with no prior data on disk (no part file),
    /// BitfieldManager.Initialize should leave bitfields empty — nothing to recover.
    /// </summary>
    [Fact]
    public async Task New_torrent_without_prior_data_has_empty_bitfields()
    {
        var tempRoot = CreateTempRoot();
        ServiceProvider? serviceProvider = null;

        try
        {
            const int pieceSize = 64;
            const int numPieces = 8;
            const long totalSize = pieceSize * numPieces;

            var metadata = CreateMetadata(pieceSize, totalSize, numPieces);
            var (settingsManager, sp) = await BuildSettingsManager(tempRoot);
            serviceProvider = sp;

            // Do NOT create the download directory or any files - simulates brand-new add
            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager);
            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);

            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);

            // No recovery result (null) - brand new torrent
            await bitfieldManager.Initialize(metadata, (RecoverableDataResult?)null);

            // Download bitfield should be completely empty
            Assert.True(bitfieldManager.TryGetDownloadBitfield(metadata.InfoHash, out var downloadBf));
            Assert.True(downloadBf.HasNone());

            // Verification bitfield should also be empty
            Assert.True(bitfieldManager.TryGetVerificationBitfield(metadata.InfoHash, out var verificationBf));
            Assert.True(verificationBf.HasNone());
        }
        finally
        {
            serviceProvider?.Dispose();
            CleanupTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// When a torrent has no recovery data indicated (HasRecoverableData = false),
    /// no pieces should be marked in the download bitfield.
    /// </summary>
    [Fact]
    public async Task Recovery_result_with_no_recoverable_data_leaves_bitfields_empty()
    {
        var tempRoot = CreateTempRoot();
        ServiceProvider? serviceProvider = null;

        try
        {
            const int pieceSize = 64;
            const int numPieces = 4;
            const long totalSize = pieceSize * numPieces;

            var metadata = CreateMetadata(pieceSize, totalSize, numPieces);
            var (settingsManager, sp) = await BuildSettingsManager(tempRoot);
            serviceProvider = sp;

            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager);
            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);

            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);

            var recoveryResult = new RecoverableDataResult
            {
                HasRecoverableData = false,
                PartFileLength = 0,
                Reason = "Part file not found"
            };

            await bitfieldManager.Initialize(metadata, recoveryResult);

            Assert.True(bitfieldManager.TryGetDownloadBitfield(metadata.InfoHash, out var downloadBf));
            Assert.True(downloadBf.HasNone());
        }
        finally
        {
            serviceProvider?.Dispose();
            CleanupTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// When persisted bitfield files already exist on disk (from a prior session),
    /// recovery logic should NOT overwrite them. The persisted download bitfield
    /// indicates the client already knew which pieces were downloaded, so HasNone()
    /// returns false and the recovery candidate-marking branch is skipped.
    /// </summary>
    [Fact]
    public async Task Persisted_bitfields_are_not_corrupted_by_recovery()
    {
        var tempRoot = CreateTempRoot();
        ServiceProvider? serviceProvider = null;

        try
        {
            const int pieceSize = 32;
            const int numPieces = 8;
            const long totalSize = pieceSize * numPieces;

            var metadata = CreateMetadata(pieceSize, totalSize, numPieces);
            var (settingsManager, sp) = await BuildSettingsManager(tempRoot);
            serviceProvider = sp;

            var downloadDir = SetupDownloadDirectory(tempRoot, metadata);

            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager);

            // Pre-create persisted download bitfield with pieces 0 and 3 marked
            var dbfPath = await fileService.GetDownloadBitFieldPath(metadata.InfoHash);
            await WriteBitfieldFile(dbfPath, numPieces, [0, 3]);

            // Pre-create persisted verification bitfield with piece 0 already verified
            var vbfPath = await fileService.GetVerificationBitFieldPath(metadata.InfoHash);
            await WriteBitfieldFile(vbfPath, numPieces, [0]);

            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);

            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);

            // Pass recovery result - but persisted bitfield has data, so recovery skip should kick in
            var recoveryResult = new RecoverableDataResult
            {
                HasRecoverableData = true,
                PartFileLength = totalSize,
                Reason = "Part file found"
            };

            await bitfieldManager.Initialize(metadata, recoveryResult);

            // Download bitfield should reflect the persisted state (pieces 0 and 3),
            // NOT the recovery scan (which would mark all 8 pieces)
            Assert.True(bitfieldManager.TryGetDownloadBitfield(metadata.InfoHash, out var downloadBf));
            Assert.True(downloadBf.HasPiece(0));
            Assert.False(downloadBf.HasPiece(1));
            Assert.False(downloadBf.HasPiece(2));
            Assert.True(downloadBf.HasPiece(3));

            // Verification bitfield should reflect persisted state (piece 0 verified)
            Assert.True(bitfieldManager.TryGetVerificationBitfield(metadata.InfoHash, out var verificationBf));
            Assert.True(verificationBf.HasPiece(0));
            Assert.False(verificationBf.HasPiece(1));
        }
        finally
        {
            serviceProvider?.Dispose();
            CleanupTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// When a part file covers all pieces, all pieces should be marked as downloaded
    /// so they get queued for validation.
    /// </summary>
    [Fact]
    public async Task Full_part_file_marks_all_pieces_for_validation()
    {
        var tempRoot = CreateTempRoot();
        ServiceProvider? serviceProvider = null;

        try
        {
            const int pieceSize = 16;
            const int numPieces = 4;
            const long totalSize = pieceSize * numPieces;

            var metadata = CreateMetadata(pieceSize, totalSize, numPieces);
            var (settingsManager, sp) = await BuildSettingsManager(tempRoot);
            serviceProvider = sp;

            var downloadDir = SetupDownloadDirectory(tempRoot, metadata);

            // Part file covers entire torrent
            var partFilePath = Path.Combine(downloadDir, $"{metadata.InfoHash.AsString()}.part");
            var partFileData = new byte[totalSize];
            new Random(99).NextBytes(partFileData);
            await File.WriteAllBytesAsync(partFilePath, partFileData);

            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager);
            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);

            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);

            var recoveryResult = new RecoverableDataResult
            {
                HasRecoverableData = true,
                PartFileLength = totalSize,
                Reason = "Full part file"
            };

            await bitfieldManager.Initialize(metadata, recoveryResult);

            // All pieces should be marked in the download bitfield
            Assert.True(bitfieldManager.TryGetDownloadBitfield(metadata.InfoHash, out var downloadBf));
            for (var i = 0; i < numPieces; i++)
                Assert.True(downloadBf.HasPiece(i), $"Piece {i} should be marked as downloaded");
        }
        finally
        {
            serviceProvider?.Dispose();
            CleanupTempRoot(tempRoot);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"torrential-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupTempRoot(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
            return;

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
            // SQLite or part files may briefly retain a file handle on Windows; cleanup is best-effort.
        }
    }

    private static string SetupDownloadDirectory(string tempRoot, TorrentMetadata metadata)
    {
        // Replicate the path logic from TorrentFileService/RecoverableDataDetector:
        // Path.GetFileNameWithoutExtension(GetPathSafeFileName(metadata.Name))
        // The test torrent name "salvage-regression-test" has no invalid chars or extension,
        // so it maps directly.
        var torrentName = Path.GetFileNameWithoutExtension(metadata.Name);
        var downloadDir = Path.Combine(tempRoot, "download", torrentName);
        Directory.CreateDirectory(downloadDir);
        return downloadDir;
    }

    private static async Task<(SettingsManager, ServiceProvider)> BuildSettingsManager(string tempRoot)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDbContext<TorrentialDb>(options =>
            options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));

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

        return (settingsManager, serviceProvider);
    }

    /// <summary>
    /// Writes a bitfield file matching the format used by BitfieldSyncService:
    /// [4 bytes big-endian piece count] [bitfield bytes]
    /// </summary>
    private static async Task WriteBitfieldFile(string path, int numPieces, int[] piecesMarked)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var bf = new Bitfield(numPieces);
        foreach (var piece in piecesMarked)
            bf.MarkHave(piece);

        var bitfieldBytes = bf.Bytes.ToArray();

        // 4-byte big-endian piece count prefix + bitfield bytes
        var fileData = new byte[4 + bitfieldBytes.Length];
        var header = fileData.AsSpan(0, 4);
        BitConverterExtensions.TryWriteBigEndian(header, numPieces);
        bitfieldBytes.CopyTo(fileData, 4);

        await File.WriteAllBytesAsync(path, fileData);
    }

    private static TorrentMetadata CreateMetadata(
        long pieceSize,
        long totalSize,
        int numberOfPieces)
    {
        return new TorrentMetadata
        {
            Name = "salvage-regression-test",
            UrlList = Array.Empty<string>(),
            AnnounceList = Array.Empty<string>(),
            Files =
            [
                new TorrentMetadataFile
                {
                    Id = 0,
                    Filename = "test-data.bin",
                    FileStartByte = 0,
                    FileSize = totalSize,
                    IsSelected = true
                }
            ],
            PieceSize = pieceSize,
            TotalSize = totalSize,
            InfoHash = "abcdef0123456789abcdef0123456789abcdef01",
            PieceHashesConcatenated = new byte[numberOfPieces * 20]
        };
    }
}
