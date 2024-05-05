using MassTransit;
using Torrential.Messages.Settings;
using Torrential.Settings;

namespace Torrential.Commands
{
    public class FileSettingsUpdateCommand : ICommand<FileSettingsUpdateResponse>
    {
        public required string DownloadPath { get; init; }
        public required string CompletedPath { get; init; }

    }

    public class FileSettingsUpdateResponse
    {

    }

    public class SettingsUpdateCommandHandler(SettingsManager settingsManager, IBus bus)
        : ICommandHandler<FileSettingsUpdateCommand, FileSettingsUpdateResponse>
    {
        public async Task<FileSettingsUpdateResponse> Execute(FileSettingsUpdateCommand command)
        {
            await settingsManager.SaveFileSettings(new()
            {
                DownloadPath = command.DownloadPath,
                CompletedPath = command.CompletedPath
            });

            await bus.Publish(new FileSettingsUpdatedMessage { CompletedPath = command.CompletedPath, IncompletePath = command.DownloadPath });
            return new();
        }
    }
}
