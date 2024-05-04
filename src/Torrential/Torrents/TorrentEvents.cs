using System.Text.Json.Serialization;

namespace Torrential.Torrents
{
    [JsonDerivedType(typeof(TorrentAddedEvent), "torrent_added")]
    [JsonDerivedType(typeof(TorrentStoppedEvent), "torrent_stopped")]
    [JsonDerivedType(typeof(TorrentStartedEvent), "torrent_started")]
    [JsonDerivedType(typeof(TorrentRemovedEvent), "torrent_removed")]
    [JsonDerivedType(typeof(TorrentCompleteEvent), "torrent_complete")]
    [JsonDerivedType(typeof(TorrentPieceDownloadedEvent), "piece_downloaded")]
    [JsonDerivedType(typeof(TorrentPieceVerifiedEvent), "piece_verified")]
    [JsonDerivedType(typeof(PeerConnectedEvent), "peer_connected")]
    [JsonDerivedType(typeof(PeerDisconnectedEvent), "peer_disconnected")]
    [JsonDerivedType(typeof(TorrentFileCopyStartedEvent), "file_copy_started")]
    [JsonDerivedType(typeof(TorrentFileCopyCompletedEvent), "file_copy_completed")]
    public interface ITorrentEvent
    {
        InfoHash InfoHash { get; }
    }

    public class TorrentAddedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string Name { get; init; }
        public required long PieceSize { get; set; }
        public required int NumberOfPieces { get; set; }
        public required ICollection<string> AnnounceList { get; set; } = Array.Empty<string>();
        public required ICollection<TorrentMetadataFile> Files { get; set; } = Array.Empty<TorrentMetadataFile>();
    }

    public class TorrentStoppedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class TorrentStartedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class TorrentRemovedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class TorrentCompleteEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class TorrentPieceDownloadedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required int PieceIndex { get; init; }
    }

    public class TorrentPieceVerifiedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required int PieceIndex { get; init; }
    }

    public class PeerConnectedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class PeerDisconnectedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
    }

    public class TorrentFileCopyStartedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string FileName { get; init; }
    }

    public class TorrentFileCopyCompletedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string FileName { get; init; }
    }


}
