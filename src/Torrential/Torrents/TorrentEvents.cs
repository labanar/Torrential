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
    [JsonDerivedType(typeof(PeerBitfieldReceivedEvent), "peer_bitfield_received")]
    [JsonDerivedType(typeof(TorrentBlockDownloaded), "block_downloaded")]
    [JsonDerivedType(typeof(TorrentBlockUploadedEvent), "block_uploaded")]
    [JsonDerivedType(typeof(TorrentStatsEvent), "throughput")]
    [JsonDerivedType(typeof(FileSelectionChangedEvent), "file_selection_changed")]
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
        public required long TotalSize { get; set; }
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

    public class TorrentStatsEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required double DownloadRate { get; init; }
        public required double UploadRate { get; init; }

        public required long TotalDownloaded { get; init; }

        public required long TotalUploaded { get; init; }
    }

    public class TorrentBlockDownloaded : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required int Length { get; init; }
    }


    public class TorrentBlockUploadedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required int Length { get; init; }
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
        public required float Progress { get; init; }
        public int[]? VerifiedPieces { get; init; }
    }

    public class PeerConnectedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string PeerId { get; init; }
        public required string Ip { get; init; }
        public required int Port { get; init; }
    }

    public class PeerBitfieldReceivedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string PeerId { get; init; }
        public required bool HasAllPieces { get; init; }
    }

    public class PeerDisconnectedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string PeerId { get; init; }
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

    public class FileSelectionChangedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required long[] SelectedFileIds { get; init; }
    }


}
