using Microsoft.Extensions.Logging;
using Torrential.Peers;
using Torrential.Torrents;
using Torrential.Trackers;
using Torrential.Trackers.Http;

namespace Torrential.Tests
{
    public class HttpTrackerTests
    {
        [Fact]
        public void PeerId_NewUsesConvention()
        {
            var peerId = PeerId.New;
            Span<byte> buffer = stackalloc byte[20];
            peerId.CopyTo(buffer);
            Assert.True(buffer.StartsWith("-TO0"u8));
        }

        [Fact]
        public void PeerId_GeneratesWithPrefix()
        {
            var peerId = PeerId.WithPrefix("-TO0100-"u8);
            Span<byte> buffer = stackalloc byte[20];
            peerId.CopyTo(buffer);
            Assert.True(buffer.StartsWith("-TO0100-"u8));
        }


        [Fact]
        public void PeerId_ParsesFromReadOnlySpan()
        {
            var peerId = PeerId.From("-AZ2060-0123456789AB"u8);
            Span<byte> buffer = stackalloc byte[20];
            peerId.CopyTo(buffer);
            Assert.True(buffer.SequenceEqual("-AZ2060-0123456789AB"u8));
        }


        [Fact]
        public void InfoHash_UrlEncode()
        {
            var meta = TorrentMetadataParser.FromFile("./debian-12.0.0-amd64-netinst.iso.torrent");
            var encodedHash = string.Create(60, meta.InfoHash, (span, hash) => hash.WriteUrlEncodedHash(span));
            Assert.Equal("%B8%51%47%4B%74%F6%5C%D1%9F%98%1C%72%35%90%E3%E5%20%24%2B%97", encodedHash);
        }


        [Fact]
        public async Task SuccessfulAnnounce()
        {
            var meta = TorrentMetadataParser.FromFile("./debian-12.0.0-amd64-netinst.iso.torrent");
            var client = new HttpClient();
            var loggerFactory = new LoggerFactory();
            var logger = loggerFactory.CreateLogger<HttpTrackerClient>();
            var sut = new HttpTrackerClient(client, logger);
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

            var conn = new PeerWireConnection(peerService, new System.Net.Sockets.TcpClient(), loggerFactory.CreateLogger<PeerWireConnection>());
            var result = await conn.Connect(meta.InfoHash, resp.Peers.First(), TimeSpan.FromSeconds(2));
            Assert.True(result.Success);
        }
    }
}