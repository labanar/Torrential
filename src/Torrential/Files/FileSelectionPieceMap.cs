using System.Collections.Concurrent;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Files;

/// <summary>
/// Maintains a per-torrent bitfield of allowed piece indices based on selected files.
/// When the user changes file selection, the allowed set is recomputed from metadata
/// without stopping the torrent.
///
/// Thread safety:
///   - The ConcurrentDictionary guards concurrent reads/writes of the per-torrent bitfield.
///   - The Bitfield itself uses atomic operations for reads, and recomputation replaces
///     the entire reference atomically via dictionary assignment.
/// </summary>
public sealed class FileSelectionPieceMap(TorrentMetadataCache metaCache, IFileSelectionService fileSelection)
{
    private readonly ConcurrentDictionary<InfoHash, Bitfield> _allowedPieces = new();

    /// <summary>
    /// Returns the allowed-pieces bitfield for the given torrent, or null if metadata
    /// is unavailable or all files are selected (no filtering needed).
    /// </summary>
    public Bitfield? GetAllowedPieces(InfoHash infoHash)
    {
        _allowedPieces.TryGetValue(infoHash, out var bitfield);
        return bitfield;
    }

    /// <summary>
    /// Recomputes the allowed-pieces bitfield from the current file selection.
    /// Call this at torrent startup and whenever the user changes file selection.
    /// </summary>
    public async Task Recompute(InfoHash infoHash)
    {
        if (!metaCache.TryGet(infoHash, out var meta))
            return;

        var selectedIds = await fileSelection.GetSelectedFileIds(infoHash);

        // If all files are selected, remove the filter entirely (no restriction)
        if (selectedIds.Count >= meta.Files.Count)
        {
            _allowedPieces.TryRemove(infoHash, out _);
            return;
        }

        // If no files are selected, create an empty bitfield (block everything)
        if (selectedIds.Count == 0)
        {
            _allowedPieces[infoHash] = new Bitfield(meta.NumberOfPieces);
            return;
        }

        var allowed = new Bitfield(meta.NumberOfPieces);
        var pieceSize = meta.PieceSize;

        foreach (var file in meta.Files)
        {
            if (!selectedIds.Contains(file.Id))
                continue;

            // Compute the piece range that this file spans
            int firstPiece = (int)(file.FileStartByte / pieceSize);
            int lastPiece = (int)((file.FileStartByte + file.FileSize - 1) / pieceSize);

            // Clamp to valid range
            if (lastPiece >= meta.NumberOfPieces)
                lastPiece = meta.NumberOfPieces - 1;

            for (int i = firstPiece; i <= lastPiece; i++)
                allowed.MarkHave(i);
        }

        _allowedPieces[infoHash] = allowed;
    }

    public void Remove(InfoHash infoHash)
    {
        _allowedPieces.TryRemove(infoHash, out _);
    }
}
