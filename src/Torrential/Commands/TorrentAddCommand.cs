using Torrential.Files;
using Torrential.Settings;
using Torrential.Torrents;

namespace Torrential.Commands
{
    public class TorrentAddCommand : ICommand<TorrentAddResponse>
    {
        public required TorrentMetadata Metadata { get; init; }
        public required string DownloadPath { get; init; }
        public required string CompletedPath { get; init; }
        public long[]? SelectedFileIds { get; init; }
    }

    public class TorrentAddResponse
    {
        public InfoHash InfoHash { get; init; }
    }

    public class TorrentAddCommandHandler(TorrentialDb db, TorrentTaskManager mgr, IMetadataFileService metaFileService, SettingsManager settingsManager)
        : ICommandHandler<TorrentAddCommand, TorrentAddResponse>
    {
        public async Task<TorrentAddResponse> Execute(TorrentAddCommand command)
        {
            var fileSettings = await settingsManager.GetFileSettings();

            await db.AddAsync(new TorrentConfiguration
            {
                InfoHash = command.Metadata.InfoHash,
                DateAdded = DateTimeOffset.UtcNow,
                CompletedPath = string.IsNullOrEmpty(command.CompletedPath) ? fileSettings.CompletedPath : command.CompletedPath,
                DownloadPath = string.IsNullOrEmpty(command.DownloadPath) ? fileSettings.DownloadPath : command.DownloadPath,
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
