using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class PostDownloadActionExecutor(IEnumerable<IPostDownloadAction> actions, ILogger<PostDownloadActionExecutor> logger)
    {
        public async Task HandleTorrentComplete(TorrentCompleteEvent evt)
        {
            foreach (var action in actions)
            {
                logger.LogInformation("Executing post download action {Action}", action.Name);
                try
                {
                    var result = await action.ExecuteAsync(evt.InfoHash, CancellationToken.None);
                    if (!result.Success && !action.ContinueOnFailure)
                        throw new Exception($"Post download action {action.Name} failed");

                    logger.LogInformation("Post download action {Action} executed successfully", action.Name);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error executing post download action {Action}", action.Name);
                    if (!action.ContinueOnFailure)
                        throw;
                }
            }
        }
    }
}
