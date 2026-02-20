namespace Torrential.Integrations
{
    public class IntegrationHook
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public IntegrationHookType Type { get; set; }
        public TorrentEventTrigger Trigger { get; set; }
        public required string Configuration { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
    }

    public enum IntegrationHookType
    {
        Slack = 0,
        Discord = 1,
        Exec = 2
    }

    public enum TorrentEventTrigger
    {
        TorrentCompleted = 0,
        TorrentStarted = 1,
        TorrentStopped = 2,
        TorrentAdded = 3,
        TorrentRemoved = 4
    }
}
