using MassTransit;
using Microsoft.Extensions.Logging;
using Torrential.Torrents;

namespace Torrential.Pipelines
{
    public class PostDownloadActionExecutor(IEnumerable<IPostDownloadAction> actions, ILogger<PostDownloadActionExecutor> logger)
        : IConsumer<TorrentCompleteEvent>
    {
        public async Task Consume(ConsumeContext<TorrentCompleteEvent> context)
        {
            foreach (var action in actions)
            {
                logger.LogInformation("Executing post download action {Action}", action.Name);
                try
                {
                    var result = await action.ExecuteAsync(context.Message.InfoHash, context.CancellationToken);
                    if (!result.Success && !action.ContinueOnFailure)
                        throw new Exception($"Post download action {action.Name} failed");
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
