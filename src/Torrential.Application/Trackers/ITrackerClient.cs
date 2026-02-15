namespace Torrential.Application.Trackers;

public interface ITrackerClient
{
    bool IsValidAnnounceForClient(string announceUrl);
    Task<AnnounceResponse> Announce(AnnounceRequest request);
}
