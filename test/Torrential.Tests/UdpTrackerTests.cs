using Torrential.Torrents;
using Torrential.Trackers;
using Torrential.Trackers.Udp;

namespace Torrential.Tests;

public class UdpTrackerTests
{
    [Fact]
    public async Task SuccessfulAnnounce()
    {
        var meta = TorrentMetadataParser.FromFile("./manjaro-kde-22.1.3-230529-linux61.iso.torrent");
        var sut = new UdpTrackerClient();
        var peerService = new PeerService();

        var resp = await sut.Announce(new AnnounceRequest
        {
            InfoHash = meta.InfoHash,
            PeerId = peerService.Self.Id,
            Url = meta.AnnounceList.First(),
            NumWant = 50,
        });

        Assert.NotEqual(0, resp.Interval);
        Assert.NotEmpty(resp.Peers);
    }
}
