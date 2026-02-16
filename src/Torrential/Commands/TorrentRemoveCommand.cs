using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Torrential.Files;
using Torrential.Peers;
using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentRemoveCommand : ICommand<TorrentRemoveResponse>
    {
        public string InfoHash { get; set; }
        public bool DeleteFiles { get; init; }
    }

    public class TorrentRemoveResponse
    {

    }

    public class TorrentRemoveCommandHandler(
        BitfieldManager bitMgr,
        TorrentMetadataCache metaCache,
        TorrentTaskManager mgr,
        TorrentialDb db,
        TorrentEventBus eventBus,
        TorrentFileService fileService,
        IFileHandleProvider fileHandleProvider,
        TorrentStats stats,
        ILogger<TorrentRemoveCommandHandler> logger)
        : ICommandHandler<TorrentRemoveCommand, TorrentRemoveResponse>
    {
        public async Task<TorrentRemoveResponse> Execute(TorrentRemoveCommand command)
        {
            await db.Torrents.Where(x => x.InfoHash == command.InfoHash).ExecuteDeleteAsync();
            await mgr.Remove(command.InfoHash);
            bitMgr.RemoveBitfields(command.InfoHash);
            fileHandleProvider.RemovePartFileHandle(command.InfoHash);
            await stats.ClearStats(command.InfoHash);

            if (command.DeleteFiles)
            {
                var metadataFilePath = await fileService.GetMetadataFilePath(command.InfoHash);
                var folder = Path.GetDirectoryName(metadataFilePath);
                if (!string.IsNullOrEmpty(folder))
                {
                    logger.LogInformation("Deleting folder {Folder}", folder);
                    Directory.Delete(folder, true);
                }
            }

            //await fileService.ClearData(command.InfoHash);
            await eventBus.PublishTorrentRemoved(new TorrentRemovedEvent { InfoHash = command.InfoHash });
            return new() { };
        }
    }
}
