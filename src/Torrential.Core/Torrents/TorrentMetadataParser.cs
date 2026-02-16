using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace Torrential.Core.Torrents;

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
            var startByte = 0L;
            files = new TorrentMetadataFile[torrent.Files.Count];
            for (var i = 0; i < torrent.Files.Count; i++)
            {
                files[i] = new TorrentMetadataFile
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
            files =
            [
                new TorrentMetadataFile
                {
                    Id = 0,
                    FileStartByte = 0,
                    Filename = torrent.File.FileName,
                    FileSize = torrent.TotalSize,
                }
            ];
        }
        else
        {
            throw new ArgumentException("Torrent must include one of File or Files");
        }

        var torrentDictionary = parser.Parse<BDictionary>(fs);
        var urlList = Array.Empty<string>();
        if (torrentDictionary.TryGetValue("url-list", out var bObject))
        {
            urlList = bObject switch
            {
                BList bUrlList => ParseUrlList(bUrlList),
                BString bUrlString when !string.IsNullOrEmpty(bUrlString?.ToString()) => [bUrlString.ToString()],
                _ => Array.Empty<string>()
            };
        }

        var trackerList = new List<string>();
        foreach (var tier in torrent.Trackers)
            foreach (var tracker in tier)
                trackerList.Add(tracker);

        return new TorrentMetadata
        {
            Name = torrent.DisplayName,
            AnnounceList = trackerList.ToArray(),
            Files = files,
            PieceSize = torrent.PieceSize,
            InfoHash = torrent.OriginalInfoHashBytes,
            UrlList = urlList,
            PieceHashesConcatenated = torrent.Pieces,
            TotalSize = torrent.TotalSize
        };
    }

    private static string[] ParseUrlList(BList? bUrlList)
    {
        if (bUrlList == null || bUrlList.Count == 0)
            return Array.Empty<string>();

        var result = new List<string>(bUrlList.Count);
        foreach (var item in bUrlList)
        {
            var str = item?.ToString();
            if (!string.IsNullOrEmpty(str))
                result.Add(str);
        }
        return result.ToArray();
    }
}
