namespace Torrential.Core.Trackers;

public class AnnounceResponse
{
    public required int Interval { get; init; }
    public int? MinInterval { get; init; }
    public string TrackerId { get; init; }
    public int Complete { get; init; }
    public int Incomplete { get; init; }
    public required ICollection<PeerInfo> Peers { get; init; }
}
