using BencodeNET.Objects;
using BencodeNET.Parsing;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Web;

namespace Torrential.Trackers.Http;

/// <summary>
/// https://www.bittorrent.org/beps/bep_0003.html
/// </summary>
public class HttpTrackerClient : ITrackerClient
{
    private readonly HttpClient _client;
    private readonly ILogger<HttpTrackerClient> _logger;

    public HttpTrackerClient(HttpClient client, ILogger<HttpTrackerClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public bool IsValidAnnounceForClient(string announceUrl)
    {
        return (announceUrl.StartsWith("http://") || announceUrl.StartsWith("https://")) && !announceUrl.Contains("ipv6");
    }

    private bool TryParsePeers(IBObject bPeers, out PeerInfo[] peers)
    {
        peers = Array.Empty<PeerInfo>();
        return bPeers switch
        {
            BString bPeerString => TryParsePeerString(bPeerString, out peers),
            BList bPeersList => TryParsePeerList(bPeersList, out peers),
            _ => false
        };
    }

    private static bool TryParsePeerString(BString bPeersString, out PeerInfo[] peers)
    {
        try
        {
            var numOfPeers = bPeersString.Length / 6;
            peers = new PeerInfo[numOfPeers];

            Span<byte> portBytes = stackalloc byte[2];
            for (int i = 0; i < numOfPeers; i++)
            {
                var ipSlice = bPeersString.Value.Span.Slice(i * 6, 4);
                bPeersString.Value.Span.Slice(i * 6 + 4, 2).CopyTo(portBytes);
                peers[i] = new PeerInfo
                {
                    Ip = new IPAddress(ipSlice),
                    Port = portBytes.ReadBigEndianUInt16()
                };
            }

            return true;
        }
        catch
        {
            peers = Array.Empty<PeerInfo>();
            return false;
        }
    }

    private static bool TryParsePeerList(BList bPeersList, out PeerInfo[] peers)
    {
        try
        {
            peers = new PeerInfo[bPeersList.Count];
            for (int i = 0; i < bPeersList.Count; i++)
            {
                var peer = bPeersList[i] as BDictionary;
                peers[i] = new PeerInfo
                {
                    Ip = new IPAddress(peer.Get<BString>("ip").Value.Span),
                    Port = (int)peer.Get<BNumber>("port").Value
                };
            }

            return true;
        }
        catch
        {
            peers = Array.Empty<PeerInfo>();
            return false;
        }
    }

    public string UrlEncodeHash(in byte[] hash)
    {
        StringBuilder encoded = new StringBuilder();

        foreach (byte b in hash)
        {
            encoded.AppendFormat("%{0:X2}", b);
        }

        return encoded.ToString();
    }

    public async Task<AnnounceResponse> Announce(AnnounceRequest request)
    {
        var info_hash = string.Create(60, request.InfoHash, (span, hash) =>
        {
            hash.WriteUrlEncodedHash(span);
        });
        var peer_id = HttpUtility.UrlEncode(request.PeerId.ToAsciiString());


        using var response = await _client.GetAsync($"{request.Url}?info_hash={info_hash}&port={request.Port}&peer_id={peer_id}&numwant={request.NumWant}&no_peer_id=1&compact=1&downloaded={request.BytesDownloaded}&uploaded={request.BytesUploaded}&left={request.BytesRemaining}");

        var rawContent = await response.Content.ReadAsStringAsync();
        var parser = new BencodeParser();
        var responseDictionary = parser.Parse<BDictionary>(await response.Content.ReadAsStreamAsync());

        var interval = responseDictionary.Get<BNumber>("interval")?.Value ?? 0;
        var seeders = responseDictionary.Get<BNumber>("complete")?.Value ?? 0;
        var leechers = responseDictionary.Get<BNumber>("incomplete")?.Value ?? 0;
        var trackerId = responseDictionary.Get<BString>("tracker id")?.ToString() ?? "";

        if (!responseDictionary.TryGetValue("peers", out var bPeers))
            throw new Exception("Announce response did not contain peers");

        if (!TryParsePeers(bPeers, out var peers))
            throw new Exception("Failed to parse peers");


        return new AnnounceResponse
        {
            Interval = (int)interval,
            Peers = peers,
            Complete = (int)seeders,
            Incomplete = (int)leechers,
            TrackerId = trackerId
        };
    }
}
