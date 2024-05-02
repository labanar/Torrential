namespace Torrential.Messages.Settings
{
    internal class FileSettingsUpdatedMessage
    {
        public required string IncompletePath { get; init; }
        public required string CompletedPath { get; init; }
    }
}
