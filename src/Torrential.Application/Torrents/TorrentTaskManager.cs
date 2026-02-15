using System.Collections.Concurrent;
using Torrential.Application.Events;
using Torrential.Application.Peers;
using Torrential.Application.Utilities;

namespace Torrential.Application.Torrents;

public class TorrentTaskManager(TorrentMetadataCache metaCache, IPeerSwarm swarms, IEventBus eventBus, BitfieldManager bitfieldManager)
{
    private ConcurrentDictionary<InfoHash, string> Torrents = [];
    private ConcurrentDictionary<InfoHash, Task> TorrentTasks = [];

    public async Task<TorrentManagerResponse> Add(TorrentMetadata torrentMetadata)
    {
        if (Torrents.ContainsKey(torrentMetadata.InfoHash))
        {
            return new()
            {
                InfoHash = torrentMetadata.InfoHash,
                Error = TorrentManagerErrorCode.TORRENT_ALREADY_EXISTS,
                Success = false
            };
        }

        metaCache.Add(torrentMetadata);
        await bitfieldManager.Initialize(torrentMetadata);

        await eventBus.PublishAsync(new TorrentAddedEvent
        {
            InfoHash = torrentMetadata.InfoHash,
            AnnounceList = torrentMetadata.AnnounceList,
            TotalSize = torrentMetadata.TotalSize,
            Files = torrentMetadata.Files,
            Name = torrentMetadata.Name,
            NumberOfPieces = torrentMetadata.NumberOfPieces,
            PieceSize = torrentMetadata.PieceSize
        });
        Torrents[torrentMetadata.InfoHash] = "Idle";
        return new() { InfoHash = torrentMetadata.InfoHash, Success = true };
    }

    public async Task<TorrentManagerResponse> Start(InfoHash infoHash)
    {
        if (!Torrents.TryGetValue(infoHash, out _))
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                Success = false
            };
        }

        if (TorrentTasks.TryGetValue(infoHash, out var torrentTask) && torrentTask.InProgress())
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_ALREADY_STARTED,
                Success = false
            };
        }

        TorrentTasks[infoHash] = swarms.MaintainSwarm(infoHash);
        await eventBus.PublishAsync(new TorrentStartedEvent { InfoHash = infoHash });
        return new()
        {
            InfoHash = infoHash,
            Success = true
        };
    }

    public async Task<TorrentManagerResponse> Stop(InfoHash infoHash)
    {
        if (!Torrents.TryGetValue(infoHash, out _))
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                Success = false
            };
        }

        if (!TorrentTasks.TryRemove(infoHash, out var torrentTask))
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                Success = false
            };
        }

        await swarms.RemoveSwarm(infoHash);
        while (torrentTask.InProgress())
            await Task.Delay(500);

        await eventBus.PublishAsync(new TorrentStoppedEvent { InfoHash = infoHash });

        return new()
        {
            InfoHash = infoHash,
            Success = true
        };
    }

    public async Task<TorrentManagerResponse> Remove(InfoHash infoHash)
    {
        if (!Torrents.TryRemove(infoHash, out _))
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                Success = false
            };
        }

        if (!TorrentTasks.TryRemove(infoHash, out var torrentTask))
        {
            return new()
            {
                InfoHash = infoHash,
                Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                Success = false
            };
        }

        await swarms.RemoveSwarm(infoHash);
        while (torrentTask.InProgress())
            await Task.Delay(500);

        await eventBus.PublishAsync(new TorrentRemovedEvent { InfoHash = infoHash });
        return new() { InfoHash = infoHash, Success = true };
    }
}

public class TorrentManagerResponse
{
    public required string InfoHash { get; init; }
    public required bool Success { get; init; }
    public TorrentManagerErrorCode Error { get; init; }
}


public enum TorrentManagerErrorCode
{
    TORRENT_ALREADY_EXISTS = 1000,
    TORRENT_ALREADY_STARTED = 1002,
    TORRENT_ALREADY_STOPPED = 1003,
    TORRENT_NOT_FOUND = 1004,
}


public static class TorrentManagerErrorMessages
{
    public static readonly Dictionary<TorrentManagerErrorCode, string> EN = new Dictionary<TorrentManagerErrorCode, string>()
    {
        { TorrentManagerErrorCode.TORRENT_ALREADY_EXISTS, "Torrent already exists" }
    };
}
