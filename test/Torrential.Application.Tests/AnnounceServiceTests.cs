using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Torrential.Application;
using Torrential.Core;
using Torrential.Core.Trackers;
using Xunit;

namespace Torrential.Application.Tests;

public class AnnounceServiceTests
{
    private static readonly InfoHash TestInfoHash = InfoHash.FromHexString("0123456789ABCDEF0123456789ABCDEF01234567");
    private static readonly PeerId TestPeerId = PeerId.New;

    private static TorrentMetaInfo CreateTestMetaInfo(params string[] announceUrls) => new()
    {
        InfoHash = TestInfoHash,
        Name = "Test Torrent",
        TotalSize = 1024 * 1024,
        PieceSize = 256 * 1024,
        NumberOfPieces = 4,
        Files = new List<TorrentFileInfo> { new(0, "file1.txt", 1024 * 1024) },
        AnnounceUrls = announceUrls.ToList(),
        PieceHashes = new byte[80]
    };

    private static AnnounceParams CreateTestParams() => new(TestPeerId, 6881);

    private static AnnounceResponse CreateTestResponse(int interval = 1800) => new()
    {
        Interval = interval,
        Peers = new List<PeerInfo> { new(IPAddress.Loopback, 6882) },
        Complete = 10,
        Incomplete = 5
    };

    private class StubTrackerClient(string prefix, Func<AnnounceRequest, Task<AnnounceResponse>> handler) : ITrackerClient
    {
        public bool IsValidAnnounceForClient(string announceUrl) => announceUrl.StartsWith(prefix);
        public Task<AnnounceResponse> Announce(AnnounceRequest request) => handler(request);
    }

    [Fact]
    public async Task AnnounceAsync_HttpTracker_ReturnsResponse()
    {
        var expected = CreateTestResponse();
        var httpClient = new StubTrackerClient("http", _ => Task.FromResult(expected));
        var service = new AnnounceService([httpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo("http://tracker.example.com/announce"),
            CreateTestParams());

        Assert.Single(results);
        Assert.Equal(expected.Interval, results[0].Interval);
    }

    [Fact]
    public async Task AnnounceAsync_UdpTracker_ReturnsResponse()
    {
        var expected = CreateTestResponse();
        var udpClient = new StubTrackerClient("udp", _ => Task.FromResult(expected));
        var service = new AnnounceService([udpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo("udp://tracker.example.com:1337/announce"),
            CreateTestParams());

        Assert.Single(results);
        Assert.Equal(expected.Interval, results[0].Interval);
    }

    [Fact]
    public async Task AnnounceAsync_NoMatchingClient_ReturnsEmptyList()
    {
        var httpClient = new StubTrackerClient("http", _ => Task.FromResult(CreateTestResponse()));
        var service = new AnnounceService([httpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo("wss://tracker.example.com/announce"),
            CreateTestParams());

        Assert.Empty(results);
    }

    [Fact]
    public async Task AnnounceAsync_MultipleTrackers_ReturnsAllResponses()
    {
        var httpResponse = CreateTestResponse(1800);
        var udpResponse = CreateTestResponse(900);
        var httpClient = new StubTrackerClient("http", _ => Task.FromResult(httpResponse));
        var udpClient = new StubTrackerClient("udp", _ => Task.FromResult(udpResponse));
        var service = new AnnounceService([httpClient, udpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo("http://tracker1.example.com/announce", "udp://tracker2.example.com:1337/announce"),
            CreateTestParams());

        Assert.Equal(2, results.Count);
        Assert.Equal(1800, results[0].Interval);
        Assert.Equal(900, results[1].Interval);
    }

    [Fact]
    public async Task AnnounceAsync_TrackerThrows_ContinuesWithRemaining()
    {
        var expected = CreateTestResponse();
        var failingClient = new StubTrackerClient("http", _ => throw new Exception("Connection refused"));
        var udpClient = new StubTrackerClient("udp", _ => Task.FromResult(expected));
        var service = new AnnounceService([failingClient, udpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo("http://bad-tracker.example.com/announce", "udp://good-tracker.example.com:1337/announce"),
            CreateTestParams());

        Assert.Single(results);
        Assert.Equal(expected.Interval, results[0].Interval);
    }

    [Fact]
    public async Task AnnounceAsync_NoAnnounceUrls_ReturnsEmptyList()
    {
        var httpClient = new StubTrackerClient("http", _ => Task.FromResult(CreateTestResponse()));
        var service = new AnnounceService([httpClient], NullLogger<AnnounceService>.Instance);

        var results = await service.AnnounceAsync(
            CreateTestMetaInfo(),
            CreateTestParams());

        Assert.Empty(results);
    }
}
