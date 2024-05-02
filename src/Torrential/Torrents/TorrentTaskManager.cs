using MassTransit;
using System.Collections.Concurrent;

namespace Torrential.Torrents
{
    public class TorrentTaskManager(TorrentMetadataCache metaCache, TorrentRunner runner, IBus bus)
    {
        private static ConcurrentDictionary<InfoHash, string> Torrents = [];
        private static ConcurrentDictionary<InfoHash, Task> TorrentTasks = [];
        private static ConcurrentDictionary<InfoHash, CancellationTokenSource> TorrentTaskCancellationTokenSources = [];

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


            await bus.Publish(new TorrentAddedEvent
            {
                InfoHash = torrentMetadata.InfoHash,
                AnnounceList = torrentMetadata.AnnounceList,
                Files = torrentMetadata.Files,
                Name = torrentMetadata.Name,
                NumberOfPieces = torrentMetadata.NumberOfPieces,
                PieceSize = torrentMetadata.PieceSize
            });
            Torrents[torrentMetadata.InfoHash] = "Idle";
            return new() { InfoHash = torrentMetadata.InfoHash, Success = true };
        }

        public async Task<TorrentManagerResponse> Remove(InfoHash infoHash)
        {
            if (!Torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            await Stop(infoHash);
            Torrents.Remove(infoHash, out _);
            await bus.Publish(new TorrentRemovedEvent { InfoHash = infoHash });
            return new() { InfoHash = infoHash, Success = true };
        }


        public async Task<TorrentManagerResponse> Start(InfoHash infoHash)
        {
            if (!Torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            if (TorrentTasks.ContainsKey(infoHash))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_ALREADY_STARTED,
                    Success = false
                };
            }

            var cts = new CancellationTokenSource();
            TorrentTaskCancellationTokenSources[infoHash] = cts;
            TorrentTasks[infoHash] = runner.Run(infoHash, cts.Token);
            await bus.Publish(new TorrentStartedEvent { InfoHash = infoHash });
            return new()
            {
                InfoHash = infoHash,
                Success = true
            };
        }


        public async Task<TorrentManagerResponse> Stop(InfoHash infoHash)
        {
            if (!Torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            if (!TorrentTaskCancellationTokenSources.ContainsKey(infoHash))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_ALREADY_STOPPED,
                    Success = false
                };
            }

            var cts = TorrentTaskCancellationTokenSources[infoHash];
            cts.Cancel();

            while (!TorrentTasks[infoHash].IsCompleted)
            {
                await Task.Delay(50);
            }


            TorrentTasks.Remove(infoHash, out _);
            TorrentTaskCancellationTokenSources.Remove(infoHash, out _);
            await bus.Publish(new TorrentStoppedEvent { InfoHash = infoHash });

            return new()
            {
                InfoHash = infoHash,
                Success = true
            };
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
}
