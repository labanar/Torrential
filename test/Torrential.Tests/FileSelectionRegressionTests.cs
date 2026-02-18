using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Commands;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Tests;

public class FileSelectionRegressionTests
{
    [Fact]
    public void Selected_file_ids_are_preserved_in_metadata()
    {
        var metadata = CreateMetadata(
            pieceSize: 8,
            totalSize: 24,
            numberOfPieces: 3,
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "a.bin", FileStartByte = 0, FileSize = 8, IsSelected = true },
                new TorrentMetadataFile { Id = 1, Filename = "b.bin", FileStartByte = 8, FileSize = 8, IsSelected = true },
                new TorrentMetadataFile { Id = 2, Filename = "c.bin", FileStartByte = 16, FileSize = 8, IsSelected = true }
            ]);

        var applySelection = typeof(TorrentAddCommandHandler).GetMethod(
            "ApplyFileSelection",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(applySelection);

        applySelection!.Invoke(null, [metadata, new long[] { 1 }]);

        Assert.False(metadata.Files.Single(x => x.Id == 0).IsSelected);
        Assert.True(metadata.Files.Single(x => x.Id == 1).IsSelected);
        Assert.False(metadata.Files.Single(x => x.Id == 2).IsSelected);
    }

    [Fact]
    public void Selected_total_size_uses_only_selected_files()
    {
        var metadata = CreateMetadata(
            pieceSize: 8,
            totalSize: 24,
            numberOfPieces: 3,
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "a.bin", FileStartByte = 0, FileSize = 8, IsSelected = true },
                new TorrentMetadataFile { Id = 1, Filename = "b.bin", FileStartByte = 8, FileSize = 8, IsSelected = false },
                new TorrentMetadataFile { Id = 2, Filename = "c.bin", FileStartByte = 16, FileSize = 8, IsSelected = true }
            ]);

        Assert.Equal(16, metadata.SelectedTotalSize);
    }

    [Fact]
    public void Wanted_pieces_map_selected_ranges_with_shared_boundaries_and_final_partial_piece()
    {
        var metadata = CreateMetadata(
            pieceSize: 8,
            totalSize: 33,
            numberOfPieces: 5,
            files:
            [
                // Selected file: spans pieces 0 and 1.
                new TorrentMetadataFile { Id = 0, Filename = "a.bin", FileStartByte = 0, FileSize = 9, IsSelected = true },
                // Unselected file: spans pieces 1 and 2 (shared boundaries with selected files).
                new TorrentMetadataFile { Id = 1, Filename = "b.bin", FileStartByte = 9, FileSize = 8, IsSelected = false },
                // Selected file: spans pieces 2 and 3.
                new TorrentMetadataFile { Id = 2, Filename = "c.bin", FileStartByte = 17, FileSize = 15, IsSelected = true },
                // Selected final partial piece.
                new TorrentMetadataFile { Id = 3, Filename = "d.bin", FileStartByte = 32, FileSize = 1, IsSelected = true }
            ]);

        Assert.Equal([0, 1, 2, 3, 4], metadata.WantedPieces.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Piece_suggestion_ignores_pieces_from_unselected_only_files()
    {
        using var myBitfield = new Bitfield(4);
        using var wantedBitfield = new Bitfield(4);
        using var peerBitfield = new Bitfield(4);

        // Only piece 2 is wanted.
        wantedBitfield.MarkHave(2);

        // Peer initially has only piece 1 (unwanted).
        peerBitfield.MarkHave(1);
        var noWantedSuggestion = myBitfield.SuggestPieceToDownload(peerBitfield, reservationBitfield: null, availability: null, wantedBitfield);
        Assert.Null(noWantedSuggestion.Index);
        Assert.True(noWantedSuggestion.MorePiecesAvailable);

        // Once peer has wanted piece 2, that piece is suggested.
        peerBitfield.MarkHave(2);
        var wantedSuggestion = myBitfield.SuggestPieceToDownload(peerBitfield, reservationBitfield: null, availability: null, wantedBitfield);
        Assert.Equal(2, wantedSuggestion.Index);
    }

    [Fact]
    public async Task Completion_criteria_uses_wanted_pieces_not_all_pieces()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"torrential-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        ServiceProvider? serviceProvider = null;

        try
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddDbContext<TorrentialDb>(options => options.UseSqlite($"Data Source={Path.Combine(tempRoot, "settings.db")}"));
            serviceProvider = services.BuildServiceProvider();

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

            var metadata = CreateMetadata(
                pieceSize: 8,
                totalSize: 29,
                numberOfPieces: 4,
                files:
                [
                    new TorrentMetadataFile { Id = 0, Filename = "a.bin", FileStartByte = 0, FileSize = 8, IsSelected = true },   // piece 0
                    new TorrentMetadataFile { Id = 1, Filename = "b.bin", FileStartByte = 8, FileSize = 8, IsSelected = false },  // piece 1 (unselected-only)
                    new TorrentMetadataFile { Id = 2, Filename = "c.bin", FileStartByte = 16, FileSize = 13, IsSelected = true }  // pieces 2 and 3 (final partial)
                ]);

            var metadataCache = new TorrentMetadataCache();
            metadataCache.Add(metadata);

            var fileService = new TorrentFileService(metadataCache, settingsManager, serviceProvider.GetRequiredService<IServiceScopeFactory>());
            await using var eventBus = new TorrentEventBus();
            var verificationTracker = new TorrentVerificationTracker(eventBus, NullLogger<TorrentVerificationTracker>.Instance);
            var bitfieldManager = new BitfieldManager(fileService, eventBus, metadataCache, verificationTracker, NullLogger<BitfieldManager>.Instance);
            await bitfieldManager.Initialize(metadata);

            Assert.True(bitfieldManager.TryGetVerificationBitfield(metadata.InfoHash, out var verificationBitfield));

            // Missing a wanted piece => not complete.
            verificationBitfield.MarkHave(0);
            verificationBitfield.MarkHave(2);
            Assert.False(bitfieldManager.HasAllWantedPieces(metadata.InfoHash, verificationBitfield));

            // All wanted pieces verified => complete, even though piece 1 is still missing.
            verificationBitfield.MarkHave(3);
            Assert.True(bitfieldManager.HasAllWantedPieces(metadata.InfoHash, verificationBitfield));
            Assert.InRange(bitfieldManager.GetWantedCompletionRatio(metadata.InfoHash, verificationBitfield), 0.999f, 1.001f);
            Assert.False(verificationBitfield.HasAll());
        }
        finally
        {
            serviceProvider?.Dispose();
            if (Directory.Exists(tempRoot))
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (IOException)
                {
                    // SQLite may briefly retain a file handle on Windows; cleanup is best-effort.
                }
            }
        }
    }

    private static TorrentMetadata CreateMetadata(
        long pieceSize,
        long totalSize,
        int numberOfPieces,
        TorrentMetadataFile[] files)
    {
        return new TorrentMetadata
        {
            Name = "selection-regression",
            UrlList = Array.Empty<string>(),
            AnnounceList = Array.Empty<string>(),
            Files = files,
            PieceSize = pieceSize,
            TotalSize = totalSize,
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            PieceHashesConcatenated = new byte[numberOfPieces * 20]
        };
    }
}
