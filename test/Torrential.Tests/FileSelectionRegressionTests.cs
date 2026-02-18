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
    /// <summary>
    /// In-memory IFileSelectionService that captures what was persisted during the add flow.
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
    }

    private static readonly MethodInfo ApplyFileSelectionMethod =
        typeof(TorrentAddCommandHandler).GetMethod(
            "ApplyFileSelection",
            BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// Invokes the private static ApplyFileSelection method on TorrentAddCommandHandler.
    /// </summary>
    private static void InvokeApplyFileSelection(TorrentMetadata metadata, IReadOnlyCollection<long>? selectedFileIds)
    {
        ApplyFileSelectionMethod.Invoke(null, [metadata, selectedFileIds]);
    }

    /// <summary>
    /// Replicates the persistence step from TorrentAddCommandHandler.Execute:
    /// persists the effective file selection (files where IsSelected == true) to the service.
    /// </summary>
    private static async Task PersistEffectiveSelection(TorrentMetadata metadata, IFileSelectionService service)
    {
        var selectedFileIds = metadata.Files.Where(f => f.IsSelected).Select(f => f.Id).ToArray();
        await service.SetSelectedFileIds(metadata.InfoHash, selectedFileIds);
    }

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

        InvokeApplyFileSelection(metadata, new long[] { 1 });

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

    // -----------------------------------------------------------------------
    // Add-flow selection persistence regression tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// When SelectedFileIds is a subset, the add flow must persist exactly that subset
    /// via IFileSelectionService — not all files.
    /// Exercises: ApplyFileSelection → persist IsSelected flags → verify service state.
    /// </summary>
    [Fact]
    public async Task Add_flow_persists_subset_selection()
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

        var service = new InMemoryFileSelectionService();

        // Simulate add-flow: user selects only file 1
        InvokeApplyFileSelection(metadata, new long[] { 1 });
        await PersistEffectiveSelection(metadata, service);

        var persisted = await service.GetSelectedFileIds(metadata.InfoHash);
        Assert.Single(persisted);
        Assert.Contains(1L, persisted);
        Assert.DoesNotContain(0L, persisted);
        Assert.DoesNotContain(2L, persisted);
    }

    /// <summary>
    /// When SelectedFileIds is null (backward compatibility), the add flow must persist
    /// all file IDs — every file is selected by default.
    /// </summary>
    [Fact]
    public async Task Add_flow_with_null_selection_persists_all_file_ids()
    {
        var metadata = CreateMetadata(
            pieceSize: 8,
            totalSize: 24,
            numberOfPieces: 3,
            files:
            [
                new TorrentMetadataFile { Id = 0, Filename = "a.bin", FileStartByte = 0, FileSize = 8, IsSelected = false },
                new TorrentMetadataFile { Id = 1, Filename = "b.bin", FileStartByte = 8, FileSize = 8, IsSelected = false },
                new TorrentMetadataFile { Id = 2, Filename = "c.bin", FileStartByte = 16, FileSize = 8, IsSelected = false }
            ],
            infoHash: "aabbccddee0123456789aabbccddee0123456789");

        var service = new InMemoryFileSelectionService();

        // Simulate add-flow: null selection → all files selected
        InvokeApplyFileSelection(metadata, null);
        await PersistEffectiveSelection(metadata, service);

        var persisted = await service.GetSelectedFileIds(metadata.InfoHash);
        Assert.Equal(3, persisted.Count);
        Assert.Contains(0L, persisted);
        Assert.Contains(1L, persisted);
        Assert.Contains(2L, persisted);
    }

    /// <summary>
    /// Invalid file IDs in the request (IDs that don't match any file) must not appear
    /// in the persisted selection. ApplyFileSelection only marks files that exist in
    /// metadata, and the handler persists based on IsSelected flags — so invalid IDs
    /// are naturally filtered out.
    /// </summary>
    [Fact]
    public async Task Add_flow_with_invalid_ids_persists_only_valid_ids()
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
            ],
            infoHash: "1122334455667788990011223344556677889900");

        var service = new InMemoryFileSelectionService();

        // Simulate add-flow: user selects file 1 plus a bogus ID 999
        InvokeApplyFileSelection(metadata, new long[] { 1, 999 });
        await PersistEffectiveSelection(metadata, service);

        // Only the valid ID (1) should be present; 999 never matched a file so it was not persisted
        var persisted = await service.GetSelectedFileIds(metadata.InfoHash);
        Assert.Contains(1L, persisted);
        Assert.DoesNotContain(999L, persisted);
        Assert.DoesNotContain(0L, persisted);
        Assert.DoesNotContain(2L, persisted);
    }

    // -----------------------------------------------------------------------
    // Detail-pane contract regression tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// The detail-pane file list must reflect the persisted IFileSelectionService state,
    /// not the in-memory metadata IsSelected flags. This ensures the info pane shows
    /// the correct checkboxes after a partial add selection.
    /// Mirrors the mapping in GET /torrents/{infoHash}/detail:
    ///   IsSelected = selectedFileIds.Contains(f.Id)
    /// </summary>
    [Fact]
    public async Task Detail_pane_files_reflect_persisted_file_selection()
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
            ],
            infoHash: "dd00dd00dd00dd00dd00dd00dd00dd00dd00dd00");

        var service = new InMemoryFileSelectionService();

        // Simulate a partial selection persisted during the add flow
        await service.SetSelectedFileIds(metadata.InfoHash, new long[] { 1 });

        var selectedFileIds = await service.GetSelectedFileIds(metadata.InfoHash);

        // Reproduce the exact mapping the detail endpoint uses
        var detailFiles = metadata.Files.Select(f => new
        {
            f.Id,
            f.Filename,
            f.FileSize,
            IsSelected = selectedFileIds.Contains(f.Id)
        }).ToArray();

        // Only file 1 should be selected — matching what was persisted, not "all files"
        Assert.False(detailFiles.Single(f => f.Id == 0).IsSelected);
        Assert.True(detailFiles.Single(f => f.Id == 1).IsSelected);
        Assert.False(detailFiles.Single(f => f.Id == 2).IsSelected);
    }

    /// <summary>
    /// After the add flow persists a partial selection, post-add toggles via the
    /// /torrents/{infoHash}/files/select endpoint should update the selection.
    /// SignalR FileSelectionChanged events carry the new selectedFileIds to the UI.
    /// This test validates the same overwrite semantics that the update endpoint +
    /// Redux updateDetailFiles reducer rely on.
    /// </summary>
    [Fact]
    public async Task Post_add_toggle_updates_persisted_selection_for_detail_pane()
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
            ],
            infoHash: "ee00ee00ee00ee00ee00ee00ee00ee00ee00ee00");

        var service = new InMemoryFileSelectionService();

        // Initial partial selection from add flow
        await service.SetSelectedFileIds(metadata.InfoHash, new long[] { 0 });

        // Post-add toggle: user also selects file 2 via the detail pane UI
        await service.SetSelectedFileIds(metadata.InfoHash, new long[] { 0, 2 });

        var selectedFileIds = await service.GetSelectedFileIds(metadata.InfoHash);

        // The detail endpoint maps isSelected from persisted selection
        var detailFiles = metadata.Files
            .Select(f => new { f.Id, IsSelected = selectedFileIds.Contains(f.Id) })
            .ToArray();

        Assert.True(detailFiles.Single(f => f.Id == 0).IsSelected);
        Assert.False(detailFiles.Single(f => f.Id == 1).IsSelected);
        Assert.True(detailFiles.Single(f => f.Id == 2).IsSelected);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static TorrentMetadata CreateMetadata(
        long pieceSize,
        long totalSize,
        int numberOfPieces,
        TorrentMetadataFile[] files,
        string infoHash = "0123456789abcdef0123456789abcdef01234567")
    {
        return new TorrentMetadata
        {
            Name = "selection-regression",
            UrlList = Array.Empty<string>(),
            AnnounceList = Array.Empty<string>(),
            Files = files,
            PieceSize = pieceSize,
            TotalSize = totalSize,
            InfoHash = infoHash,
            PieceHashesConcatenated = new byte[numberOfPieces * 20]
        };
    }
}
