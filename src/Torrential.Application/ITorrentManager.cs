using Torrential.Core;

namespace Torrential.Application;

public interface ITorrentManager
{
    /// <summary>
    /// Adds a torrent to the manager. The user can optionally select a subset of files to download.
    /// If no file selections are provided, all files are selected by default.
    /// </summary>
    TorrentManagerResult Add(TorrentMetaInfo metaInfo, IReadOnlyList<TorrentFileSelection>? fileSelections = null);

    /// <summary>
    /// Starts downloading/seeding the torrent identified by the given InfoHash.
    /// </summary>
    TorrentManagerResult Start(InfoHash infoHash);

    /// <summary>
    /// Stops the torrent identified by the given InfoHash.
    /// </summary>
    TorrentManagerResult Stop(InfoHash infoHash);

    /// <summary>
    /// Removes the torrent. If deleteData is true, also deletes any downloaded data on disk.
    /// </summary>
    TorrentManagerResult Remove(InfoHash infoHash, bool deleteData = false);

    /// <summary>
    /// Gets the current state of a torrent, or null if not found.
    /// </summary>
    TorrentState? GetState(InfoHash infoHash);

    /// <summary>
    /// Gets the current state of all managed torrents.
    /// </summary>
    IReadOnlyList<TorrentState> GetAll();
}
