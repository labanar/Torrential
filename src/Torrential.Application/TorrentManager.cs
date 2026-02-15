using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Torrential.Application.Persistence;
using Torrential.Core;

namespace Torrential.Application;

public class TorrentManager(ILogger<TorrentManager> logger, IServiceScopeFactory scopeFactory) : ITorrentManager
{
    private readonly ConcurrentDictionary<InfoHash, TorrentState> _torrents = new();
    private readonly ConcurrentDictionary<InfoHash, TorrentMetaInfo> _metaInfos = new();

    private TorrentDbContext CreateDbContext()
    {
        var scope = scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TorrentDbContext>();
    }

    private static TorrentEntity ToEntity(TorrentState state, TorrentMetaInfo metaInfo)
    {
        return new TorrentEntity
        {
            InfoHash = state.InfoHash.AsString(),
            Name = state.Name,
            TotalSize = state.TotalSize,
            PieceSize = state.PieceSize,
            NumberOfPieces = state.NumberOfPieces,
            FilesJson = JsonSerializer.Serialize(state.Files),
            SelectedFileIndicesJson = JsonSerializer.Serialize(state.SelectedFileIndices),
            AnnounceUrlsJson = JsonSerializer.Serialize(metaInfo.AnnounceUrls),
            PieceHashes = metaInfo.PieceHashes,
            Status = state.Status.ToString(),
            DateAdded = state.DateAdded
        };
    }

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

        _metaInfos.TryAdd(metaInfo.InfoHash, metaInfo);

        using var db = CreateDbContext();
        db.Torrents.Add(ToEntity(state, metaInfo));
        db.SaveChanges();

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

        using var db = CreateDbContext();
        var entity = db.Torrents.Find(infoHash.AsString());
        if (entity is not null)
        {
            entity.Status = TorrentStatus.Downloading.ToString();
            db.SaveChanges();
        }

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

        using var db = CreateDbContext();
        var entity = db.Torrents.Find(infoHash.AsString());
        if (entity is not null)
        {
            entity.Status = TorrentStatus.Stopped.ToString();
            db.SaveChanges();
        }

        logger.LogInformation("Torrent stopped: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        return TorrentManagerResult.Ok();
    }

    public TorrentManagerResult Remove(InfoHash infoHash, bool deleteData = false)
    {
        if (!_torrents.TryRemove(infoHash, out var state))
            return TorrentManagerResult.Fail(TorrentManagerError.TorrentNotFound);

        _metaInfos.TryRemove(infoHash, out _);

        using var db = CreateDbContext();
        var entity = db.Torrents.Find(infoHash.AsString());
        if (entity is not null)
        {
            db.Torrents.Remove(entity);
            db.SaveChanges();
        }

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

        using var db = CreateDbContext();
        var entity = db.Torrents.Find(infoHash.AsString());
        if (entity is not null)
        {
            entity.SelectedFileIndicesJson = JsonSerializer.Serialize(updated);
            db.SaveChanges();
        }

        logger.LogInformation("File selections updated for torrent: {Name} ({InfoHash})", state.Name, infoHash.AsString());
        return TorrentManagerResult.Ok();
    }

    internal void LoadFromEntities(IReadOnlyList<TorrentEntity> entities)
    {
        foreach (var entity in entities)
        {
            var infoHash = InfoHash.FromHexString(entity.InfoHash);
            var files = JsonSerializer.Deserialize<List<TorrentFileInfo>>(entity.FilesJson) ?? [];
            var selectedIndices = JsonSerializer.Deserialize<HashSet<int>>(entity.SelectedFileIndicesJson) ?? [];
            var announceUrls = JsonSerializer.Deserialize<List<string>>(entity.AnnounceUrlsJson) ?? [];
            var status = Enum.Parse<TorrentStatus>(entity.Status);

            var state = new TorrentState
            {
                InfoHash = infoHash,
                Name = entity.Name,
                TotalSize = entity.TotalSize,
                PieceSize = entity.PieceSize,
                NumberOfPieces = entity.NumberOfPieces,
                Files = files,
                SelectedFileIndices = selectedIndices,
                Status = status,
                DateAdded = entity.DateAdded
            };

            var metaInfo = new TorrentMetaInfo
            {
                InfoHash = infoHash,
                Name = entity.Name,
                TotalSize = entity.TotalSize,
                PieceSize = entity.PieceSize,
                NumberOfPieces = entity.NumberOfPieces,
                Files = files,
                AnnounceUrls = announceUrls,
                PieceHashes = entity.PieceHashes
            };

            _torrents.TryAdd(infoHash, state);
            _metaInfos.TryAdd(infoHash, metaInfo);
        }
    }
}
