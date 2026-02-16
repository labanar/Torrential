using Torrential.Core.Torrents;
using Torrential.Trackers;
using Torrential.Trackers.Http;

var torrentInfo = TorrentMetadataParser.FromFile(@"C:\Users\Rami\Downloads\debian-13.3.0-amd64-netinst.iso.torrent");

var tracker = new HttpTrackerClient(new(), default);
var announceRequest = new AnnounceRequest
{
    InfoHash = torrentInfo.InfoHash,
    Url = torrentInfo.UrlList.FirstOrDefault(),
    PeerId = default,
    Port = 20931
};

var resp = tracker.Announce(announceRequest);

//Get peers back

//Establish connection with peer

//Unchoke and start requesting data

Console.ReadLine();