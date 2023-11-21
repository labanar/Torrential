namespace Torrential.Persistence
{
    internal interface ITorrentPersistence
    {
        IAsyncEnumerable<TorrentPiece> GetPieces(InfoHash infoHash);
        IAsyncEnumerable<TorrentPiece> GetVerifiedPieces(InfoHash infoHash);
    }

    public class InMemoryTorrentPersistence : ITorrentPersistence
    {
        private readonly Dictionary<InfoHash, TorrentPiece> _torrentPieces = new Dictionary<InfoHash, TorrentPiece>();

        //Which pieces do I need?
        IAsyncEnumerable<TorrentPiece> ITorrentPersistence.GetPieces(InfoHash infoHash)
        {
            throw new NotImplementedException();
        }

        //Which pieces do I have?
        IAsyncEnumerable<TorrentPiece> ITorrentPersistence.GetVerifiedPieces(InfoHash infoHash)
        {
            throw new NotImplementedException();
        }
    }
}
