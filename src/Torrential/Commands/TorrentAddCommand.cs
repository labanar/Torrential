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
        public IReadOnlyCollection<long>? SelectedFileIds { get; init; }
    }

    public class TorrentAddResponse
    {
        public InfoHash InfoHash { get; init; }
    }

    public class TorrentAddCommandHandler(TorrentialDb db, TorrentTaskManager mgr, IMetadataFileService metaFileService, IFileSelectionService fileSelectionService, SettingsManager settingsManager)
        : ICommandHandler<TorrentAddCommand, TorrentAddResponse>
    {
        public async Task<TorrentAddResponse> Execute(TorrentAddCommand command)
        {
            var fileSettings = await settingsManager.GetFileSettings();
            ApplyFileSelection(command.Metadata, command.SelectedFileIds);

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

            // Default all files selected on add
            var allFileIds = command.Metadata.Files.Select(f => f.Id).ToArray();
            await fileSelectionService.SetSelectedFileIds(command.Metadata.InfoHash, allFileIds);

            await db.SaveChangesAsync();
            await mgr.Add(command.Metadata);
            return new() { InfoHash = command.Metadata.InfoHash };
        }

        private static void ApplyFileSelection(TorrentMetadata metadata, IReadOnlyCollection<long>? selectedFileIds)
        {
            // Backward compatibility: if no selection is provided, keep all files selected.
            if (selectedFileIds == null)
            {
                foreach (var file in metadata.Files)
                    file.IsSelected = true;

                return;
            }

            var selected = selectedFileIds.ToHashSet();
            foreach (var file in metadata.Files)
                file.IsSelected = selected.Contains(file.Id);
        }
    }
}
