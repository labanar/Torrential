namespace Torrential.Trackers;

public class AnnounceRequest
{
    public required string Url { get; init; }
    public required InfoHash InfoHash { get; init; }
    public required PeerId PeerId { get; init; }
    public int NumWant { get; init; } = 50;
    public long BytesDownloaded { get; init; }
    public long BytesRemaining { get; init; }
    public long BytesUploaded { get; init; }
}
