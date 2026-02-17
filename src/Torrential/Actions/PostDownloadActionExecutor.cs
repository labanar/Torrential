using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class PostDownloadActionExecutor(IEnumerable<IPostDownloadAction> actions, ILogger<PostDownloadActionExecutor> logger)
    {
        public async Task HandleTorrentComplete(TorrentCompleteEvent evt)
        {
            logger.LogInformation("Torrent complete received for {Torrent}; executing {ActionCount} post-download actions", evt.InfoHash, actions.Count());
            foreach (var action in actions)
            {
                logger.LogInformation("Executing post download action {Action} for torrent {Torrent}", action.Name, evt.InfoHash);
                try
                {
                    var result = await action.ExecuteAsync(evt.InfoHash, CancellationToken.None);
                    if (!result.Success && !action.ContinueOnFailure)
                        throw new Exception($"Post download action {action.Name} failed");

                    logger.LogInformation("Post download action {Action} executed successfully for torrent {Torrent}", action.Name, evt.InfoHash);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error executing post download action {Action} for torrent {Torrent}", action.Name, evt.InfoHash);
                    if (!action.ContinueOnFailure)
                    {
                        logger.LogWarning("Halting post-download action execution for torrent {Torrent} due to action failure: {Action}", evt.InfoHash, action.Name);
                        throw;
                    }
                }
            }

            logger.LogInformation("Finished post-download actions for torrent {Torrent}", evt.InfoHash);
        }
    }
}
