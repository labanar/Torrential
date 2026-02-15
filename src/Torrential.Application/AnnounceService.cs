using Microsoft.Extensions.Logging;
using Torrential.Core;
using Torrential.Core.Trackers;

namespace Torrential.Application;

public record AnnounceParams(
    PeerId PeerId,
    int Port,
    int NumWant = 50,
    long BytesDownloaded = 0,
    long BytesRemaining = 0,
    long BytesUploaded = 0);

public interface IAnnounceService
{
    Task<IReadOnlyList<AnnounceResponse>> AnnounceAsync(TorrentMetaInfo metaInfo, AnnounceParams announceParams, CancellationToken cancellationToken = default);
}

public class AnnounceService(IEnumerable<ITrackerClient> trackerClients, ILogger<AnnounceService> logger) : IAnnounceService
{
    public async Task<IReadOnlyList<AnnounceResponse>> AnnounceAsync(TorrentMetaInfo metaInfo, AnnounceParams announceParams, CancellationToken cancellationToken = default)
    {
        var responses = new List<AnnounceResponse>();

        foreach (var url in metaInfo.AnnounceUrls)
        {
            var client = trackerClients.FirstOrDefault(c => c.IsValidAnnounceForClient(url));
            if (client is null)
            {
                logger.LogWarning("No tracker client found for announce URL: {Url}", url);
                continue;
            }

            try
            {
                var request = new AnnounceRequest
                {
                    Url = url,
                    InfoHash = metaInfo.InfoHash,
                    PeerId = announceParams.PeerId,
                    Port = announceParams.Port,
                    NumWant = announceParams.NumWant,
                    BytesDownloaded = announceParams.BytesDownloaded,
                    BytesRemaining = announceParams.BytesRemaining,
                    BytesUploaded = announceParams.BytesUploaded
                };

                var response = await client.Announce(request);
                responses.Add(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to announce to tracker: {Url}", url);
            }
        }

        return responses;
    }
}
