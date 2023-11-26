using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Torrential.Torrents
{
    public class TorrentEventDispatcher
    {
        private static Channel<ITorrentEvent> _events = Channel.CreateUnbounded<ITorrentEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        public static ChannelWriter<ITorrentEvent> EventWriter = _events.Writer;
        public static ChannelReader<ITorrentEvent> EventReader = _events.Reader;

    }


    [JsonDerivedType(typeof(TorrentAddedEvent), "torrent_added")]
    [JsonDerivedType(typeof(TorrentStoppedEvent), "torrent_stopped")]
    [JsonDerivedType(typeof(TorrentStartedEvent), "torrent_started")]
    [JsonDerivedType(typeof(TorrentRemovedEvent), "torrent_removed")]
    [JsonDerivedType(typeof(TorrentCompleteEvent), "torrent_complete")]
    [JsonDerivedType(typeof(TorrentPieceDownloadedEvent), "piece_downloaded")]
    [JsonDerivedType(typeof(TorrentPieceVerifiedEvent), "piece_verified")]
    [JsonDerivedType(typeof(PeerConnectedEvent), "peer_connected")]
    [JsonDerivedType(typeof(PeerDisconnectedEvent), "peer_disconnected")]
    public interface ITorrentEvent
    {
        InfoHash InfoHash { get; }
    }

    public class TorrentAddedEvent : ITorrentEvent
    {
        public required InfoHash InfoHash { get; init; }
        public required string Name { get; init; }
        public required long PieceSize { get; set; }
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
}
