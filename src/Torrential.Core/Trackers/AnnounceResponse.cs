using System.Net;

namespace Torrential.Trackers;

public class AnnounceResponse
{
    public required int Interval { get; init; }
    public int? MinInterval { get; init; }
    public string TrackerId { get; init; }
    public int Complete { get; init; }
    public int Incomplete { get; init; }
    public required PeerInfo[] Peers { get; init; }
}

public class PeerInfo
{
    public required IPAddress Ip { get; init; }
    public required int Port { get; init; }
}