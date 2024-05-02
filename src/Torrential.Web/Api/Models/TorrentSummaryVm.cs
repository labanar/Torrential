namespace Torrential.Web.Api.Models
{
    public class TorrentSummaryVm
    {
        public required string Name { get; set; }
        public required float Percentage { get; set; }

        public required int ConnectedSeeds { get; set; }
        public required int ConnectedPeers { get; set; }

        public required long BytesDownloaded { get; set; }
        public required long BytesTotal { get; set; }
    }
}
