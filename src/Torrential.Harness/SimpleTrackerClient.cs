using System.Buffers.Binary;
using System.Net;
using System.Web;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using Microsoft.Extensions.Logging;
using Torrential.Core;

namespace Torrential.Harness;

public record TrackerPeer(IPAddress Ip, int Port);

public class SimpleTrackerClient
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public SimpleTrackerClient(HttpClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<TrackerPeer>> Announce(
        string announceUrl,
        InfoHash infoHash,
        PeerId peerId,
        long totalSize)
    {
        var urlEncodedHash = string.Create(60, infoHash, (span, hash) =>
        {
            hash.WriteUrlEncodedHash(span);
        });
        var urlEncodedPeerId = HttpUtility.UrlEncode(peerId.ToAsciiString());

        var url = $"{announceUrl}?info_hash={urlEncodedHash}&port=6881&peer_id={urlEncodedPeerId}&numwant=50&no_peer_id=1&compact=1&downloaded=0&uploaded=0&left={totalSize}";

        _logger.LogInformation("Announcing to tracker: {Url}", announceUrl);

        try
        {
            using var response = await _client.GetAsync(url);
            var parser = new BencodeParser();
            var dict = parser.Parse<BDictionary>(await response.Content.ReadAsStreamAsync());

            if (!dict.TryGetValue("peers", out var bPeers))
            {
                _logger.LogWarning("Tracker response did not contain peers");
                return [];
            }

            return ParsePeers(bPeers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tracker announce failed for {Url}", announceUrl);
            return [];
        }
    }

    private static List<TrackerPeer> ParsePeers(IBObject bPeers)
    {
        if (bPeers is BString bPeerString)
            return ParseCompactPeers(bPeerString);

        if (bPeers is BList bPeerList)
            return ParseDictPeers(bPeerList);

        return [];
    }

    private static List<TrackerPeer> ParseCompactPeers(BString bPeersString)
    {
        var numPeers = bPeersString.Length / 6;
        var peers = new List<TrackerPeer>(numPeers);
        var data = bPeersString.Value.Span;

        for (int i = 0; i < numPeers; i++)
        {
            var ip = new IPAddress(data.Slice(i * 6, 4));
            var port = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i * 6 + 4, 2));
            peers.Add(new TrackerPeer(ip, port));
        }

        return peers;
    }

    private static List<TrackerPeer> ParseDictPeers(BList bPeersList)
    {
        var peers = new List<TrackerPeer>(bPeersList.Count);
        for (int i = 0; i < bPeersList.Count; i++)
        {
            if (bPeersList[i] is BDictionary peer)
            {
                var ip = new IPAddress(peer.Get<BString>("ip").Value.Span);
                var port = (int)peer.Get<BNumber>("port").Value;
                peers.Add(new TrackerPeer(ip, port));
            }
        }
        return peers;
    }
}
