using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Torrential.Core;

namespace Torrential.Harness;

public record TorrentInfo(
    string Name,
    InfoHash InfoHash,
    long PieceSize,
    int NumberOfPieces,
    long TotalSize,
    IReadOnlyList<string> AnnounceUrls);

public static class TorrentFileParser
{
    public static TorrentInfo FromFile(string fileName)
    {
        using var fs = File.OpenRead(fileName);
        var parser = new BencodeParser();
        var torrent = parser.Parse<Torrent>(fs);

        var announceUrls = torrent.Trackers
            .SelectMany(x => x)
            .Where(x => x.StartsWith("http://") || x.StartsWith("https://"))
            .Where(x => !x.Contains("ipv6"))
            .ToList();

        var infoHash = InfoHash.FromSpan(torrent.OriginalInfoHashBytes);
        var numberOfPieces = torrent.Pieces.Length / 20;

        return new TorrentInfo(
            Name: torrent.DisplayName,
            InfoHash: infoHash,
            PieceSize: torrent.PieceSize,
            NumberOfPieces: numberOfPieces,
            TotalSize: torrent.TotalSize,
            AnnounceUrls: announceUrls);
    }
}
