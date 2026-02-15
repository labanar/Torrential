using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Application;

public class TorrentManager(ILogger<TorrentManager> logger) : ITorrentManager
{
    private readonly ConcurrentDictionary<InfoHash, TorrentState> _torrents = new();

    public TorrentManagerResult Add(TorrentMetaInfo metaInfo, IReadOnlyList<TorrentFileSelection>? fileSelections = null)
    {
        if (_torrents.ContainsKey(metaInfo.InfoHash))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentAlreadyExists);

        HashSet<int> selectedIndices;
        if (fileSelections is not null)
        {
            foreach (var selection in fileSelections)
            {
                if (selection.FileIndex < 0 || selection.FileIndex >= metaInfo.Files.Count)
                    return TorrentManagerResult.Fail(TorrentManagerError.InvalidFileSelection);
            }

            selectedIndices = fileSelections
                .Where(s => s.Selected)
                .Select(s => s.FileIndex)
                .ToHashSet();
        }
        else
        {
            selectedIndices = Enumerable.Range(0, metaInfo.Files.Count).ToHashSet();
        }

        var state = new TorrentState
        {
            InfoHash = metaInfo.InfoHash,
            Name = metaInfo.Name,
            TotalSize = metaInfo.TotalSize,
            PieceSize = metaInfo.PieceSize,
            NumberOfPieces = metaInfo.NumberOfPieces,
            Files = metaInfo.Files,
            SelectedFileIndices = selectedIndices,
            Status = TorrentStatus.Added
        };

        if (!_torrents.TryAdd(metaInfo.InfoHash, state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentAlreadyExists);

        logger.LogInformation("Torrent added: {Name} ({InfoHash})", metaInfo.Name, metaInfo.InfoHash.AsString());
        return TorrentManagerResult.Ok();
    }

    public TorrentManagerResult Start(InfoHash infoHash)
    {
        if (!_torrents.TryGetValue(infoHash, out var state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentNotFound);

        if (state.Status == TorrentStatus.Downloading)
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentAlreadyRunning);

        state.Status = TorrentStatus.Downloading;
        logger.LogInformation("Torrent started: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        return TorrentManagerResult.Ok();
    }

    public TorrentManagerResult Stop(InfoHash infoHash)
    {
        if (!_torrents.TryGetValue(infoHash, out var state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentNotFound);

        if (state.Status != TorrentStatus.Downloading)
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentAlreadyStopped);

        state.Status = TorrentStatus.Stopped;
        logger.LogInformation("Torrent stopped: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        return TorrentManagerResult.Ok();
    }

    public TorrentManagerResult Remove(InfoHash infoHash, bool deleteData = false)
    {
        if (!_torrents.TryRemove(infoHash, out var state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentNotFound);

        if (deleteData)
            logger.LogInformation("Torrent removed with data deletion requested: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        else
            logger.LogInformation("Torrent removed: {Name} ({InfoHash})", state.Name, infoHash.AsString());

        return TorrentManagerResult.Ok();
    }

    public TorrentState? GetState(InfoHash infoHash)
    {
        _torrents.TryGetValue(infoHash, out var state);
        return state;
    }

    public IReadOnlyList<TorrentState> GetAll()
    {
        return _torrents.Values.ToList();
    }

    public TorrentManagerResult UpdateFileSelections(InfoHash infoHash, IReadOnlyList<TorrentFileSelection> fileSelections)
    {
        if (!_torrents.TryGetValue(infoHash, out var state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentNotFound);

        foreach (var selection in fileSelections)
        {
            if (selection.FileIndex < 0 || selection.FileIndex >= state.Files.Count)
                return TorrentManagerResult.Fail(TorrentManagerError.InvalidFileSelection);
        }

        var updated = state.SelectedFileIndices.ToHashSet();
        foreach (var selection in fileSelections)
        {
            if (selection.Selected)
                updated.Add(selection.FileIndex);
            else
                updated.Remove(selection.FileIndex);
        }

        state.SelectedFileIndices = updated;
        logger.LogInformation("File selections updated for torrent: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        return TorrentManagerResult.Ok();
    }
}
