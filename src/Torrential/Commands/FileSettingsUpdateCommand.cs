using MassTransit;
using Torrential.Messages.Settings;

namespace Torrential.Commands
{
    public class FileSettingsUpdateCommand : ICommand<FileSettingsUpdateResponse>
    {
        public string DownloadPath { get; init; }
        public string CompletedPath { get; init; }

    }

    public class FileSettingsUpdateResponse
    {

    }

    public class SettingsUpdateCommandHandler(TorrentialDb db, IBus bus)
        : ICommandHandler<FileSettingsUpdateCommand, FileSettingsUpdateResponse>
    {
        public async Task<FileSettingsUpdateResponse> Execute(FileSettingsUpdateCommand command)
        {
            var settings = await db.Settings.FindAsync(TorrentialSettings.DefaultId);
            if (settings != null)
            {
                settings.FileSettings = new FileSettings
                {
                    DownloadPath = command.DownloadPath,
                    CompletedPath = command.CompletedPath
                };
            }
            else
            {
                await db.Settings.AddAsync(new()
                {
                    FileSettings = new FileSettings
                    {
                        DownloadPath = command.DownloadPath,
                        CompletedPath = command.CompletedPath
                    }
                });
            }
            await db.SaveChangesAsync();
            await bus.Publish(new FileSettingsUpdatedMessage { CompletedPath = command.CompletedPath, IncompletePath = command.DownloadPath });
            return new();
        }
    }
}
