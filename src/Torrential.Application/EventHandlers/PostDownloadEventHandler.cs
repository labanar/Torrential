using Torrential.Application.Events;
using Torrential.Application.FileCopy;

namespace Torrential.Application.EventHandlers;

public class PostDownloadEventHandler(IFileCopyService fileCopyService)
    : IEventHandler<TorrentCompleteEvent>
{
    public async Task HandleAsync(TorrentCompleteEvent @event, CancellationToken cancellationToken = default)
    {
        await fileCopyService.CopyFilesAsync(@event.InfoHash, cancellationToken);
    }
}
