using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using System.Collections.Concurrent;

namespace Torrential.Torrents;

public class TorrentMetadataCache
{
    private readonly ConcurrentDictionary<InfoHash, TorrentMetadata> _torrents = [];
    public void Add(TorrentMetadata meta)
    {
        _torrents.TryAdd(meta.InfoHash, meta);
    }

    public bool TryAdd(TorrentMetadata meta)
    {
        return _torrents.TryAdd(meta.InfoHash, meta);
    }

    public bool TryGet(InfoHash hash, out TorrentMetadata meta)
    {
        return _torrents.TryGetValue(hash, out meta);
    }
}

public static class TorrentMetadataParser
{
    public static TorrentMetadata FromFile(string fileName)
    {
        using var fs = File.OpenRead(fileName);
        return FromStream(fs);
    }

    public static TorrentMetadata FromStream(Stream fs)
    {
        var parser = new BencodeParser();
        var torrent = parser.Parse<Torrent>(fs);
        fs.Seek(0, SeekOrigin.Begin);

        var files = Array.Empty<TorrentMetadataFile>();
        if (torrent.Files != null)
        {
            files = new TorrentMetadataFile[torrent.Files.Count];
            for (var i = 0; i < torrent.Files.Count; i++)
            {
                var startByte = 0L;
                new TorrentMetadataFile
                {
                    Id = i,
                    Filename = torrent.Files[i].FullPath,
                    FileSize = torrent.Files[i].FileSize,
                    FileStartByte = startByte,
                };
                startByte += torrent.Files[i].FileSize;
            }
        }
        else if (torrent.File != null)
        {
            files = new TorrentMetadataFile[1]
            {
                new TorrentMetadataFile
                {
                    Id = 0,
                    FileStartByte = 0,
                    Filename = torrent.File.FileName,
                    FileSize = torrent.TotalSize,
                }
            };
        }
        else
        {
            throw new ArgumentException("Torrent must include one of File or Files");
        }

        var torrentDicitonary = parser.Parse<BDictionary>(fs);
        var urlList = Array.Empty<string>();
        if (torrentDicitonary.TryGetValue("url-list", out var bObject))
        {
            urlList = bObject switch
            {
                BList bUrlList => bUrlList?.Select(x => x?.ToString() ?? "")?.Where(x => !string.IsNullOrEmpty(x))?.ToArray() ?? Array.Empty<string>(),
                BString bUrlString when !string.IsNullOrEmpty(bUrlString?.ToString()) => new string[1] { bUrlString.ToString() },
                _ => Array.Empty<string>()
            };
        }

        return new TorrentMetadata
        {
            Name = torrent.DisplayName,
            AnnounceList = torrent.Trackers.SelectMany(x => x).ToArray(),
            Files = files,
            PieceSize = torrent.PieceSize,
            InfoHash = InfoHash.FromSpan(torrent.OriginalInfoHashBytes),
            UrlList = urlList,
            PieceHashesConcatenated = torrent.Pieces
        };
    }
}
