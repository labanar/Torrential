namespace Torrential.Application.Persistence;

public class SettingsEntity
{
    public int Id { get; set; } = 1;
    public string DownloadFolder { get; set; } = "/downloads";
    public string CompletedFolder { get; set; } = "/downloads/completed";
    public int MaxHalfOpenConnections { get; set; } = 50;
    public int MaxPeersPerTorrent { get; set; } = 50;
}
