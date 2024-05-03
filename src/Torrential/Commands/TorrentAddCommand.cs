using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentAddCommand : ICommand<TorrentAddResponse>
    {
        public required TorrentMetadata Metadata { get; init; }
        public required string DownloadPath { get; init; }
        public required string CompletedPath { get; init; }
    }

    public class TorrentAddResponse
    {
        public InfoHash InfoHash { get; init; }
    }

    public class TorrentAddCommandHandler(TorrentialDb db, TorrentTaskManager mgr, IMetadataFileService metaFileService)
        : ICommandHandler<TorrentAddCommand, TorrentAddResponse>
    {
        public async Task<TorrentAddResponse> Execute(TorrentAddCommand command)
        {
            await db.AddAsync(new TorrentConfiguration
            {
                InfoHash = command.Metadata.InfoHash,
                DateAdded = DateTimeOffset.UtcNow,
                CompletedPath = command.CompletedPath,
                DownloadPath = command.DownloadPath,
                Status = TorrentStatus.Idle
            });

            //Save the metadata to the file system
            await metaFileService.SaveMetadata(command.Metadata);

            await db.SaveChangesAsync();
            await mgr.Add(command.Metadata);
            return new() { InfoHash = command.Metadata.InfoHash };
        }
    }
}
