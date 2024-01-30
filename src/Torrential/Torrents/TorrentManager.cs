using MassTransit;
using System.Collections.Concurrent;

namespace Torrential.Torrents
{
    public class TorrentManager(TorrentMetadataCache metaCache, TorrentRunner runner, IBus bus)
    {
        private ConcurrentDictionary<InfoHash, string> _torrents = [];
        private ConcurrentDictionary<InfoHash, Task> _torrentTasks = [];
        private ConcurrentDictionary<InfoHash, CancellationTokenSource> _torrentTokens = [];

        public async Task<TorrentManagerResponse> Add(TorrentMetadata torrentMetadata)
        {
            if (_torrents.ContainsKey(torrentMetadata.InfoHash))
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
            _torrents[torrentMetadata.InfoHash] = "Idle";
            return new() { InfoHash = torrentMetadata.InfoHash, Success = true };
        }

        public async Task<TorrentManagerResponse> Remove(InfoHash infoHash)
        {
            if (!_torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            await Stop(infoHash);
            _torrents.Remove(infoHash, out _);
            await bus.Publish(new TorrentRemovedEvent { InfoHash = infoHash });
            return new() { InfoHash = infoHash, Success = true };
        }


        public async Task<TorrentManagerResponse> Start(InfoHash infoHash)
        {
            if (!_torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            if (_torrentTasks.ContainsKey(infoHash))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_ALREADY_STARTED,
                    Success = false
                };
            }

            var cts = new CancellationTokenSource();
            _torrentTokens[infoHash] = cts;
            _torrentTasks[infoHash] = runner.Run(infoHash, cts.Token);
            await bus.Publish(new TorrentStartedEvent { InfoHash = infoHash });
            return new()
            {
                InfoHash = infoHash,
                Success = true
            };


            //Start a long running task to essentially run this torrent
            //Create a CTS and pass it into the processor
            //Stop should technically stop everything immediately
        }


        public async Task<TorrentManagerResponse> Stop(InfoHash infoHash)
        {
            if (!_torrents.TryGetValue(infoHash, out var status))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_NOT_FOUND,
                    Success = false
                };
            }

            if (!_torrentTokens.ContainsKey(infoHash))
            {
                return new()
                {
                    InfoHash = infoHash,
                    Error = TorrentManagerErrorCode.TORRENT_ALREADY_STOPPED,
                    Success = false
                };
            }

            var cts = _torrentTokens[infoHash];
            cts.Cancel();

            while (!_torrentTasks[infoHash].IsCompleted)
            {
                await Task.Delay(50);
            }


            _torrentTasks.Remove(infoHash, out _);
            _torrentTokens.Remove(infoHash, out _);
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
