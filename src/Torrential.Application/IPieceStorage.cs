using Torrential.Core;

namespace Torrential.Application;

public interface IPieceStorage
{
    Task InitializeTorrentStorageAsync(TorrentMetaInfo metaInfo);
    Task WritePieceAsync(InfoHash infoHash, int pieceIndex, TorrentMetaInfo metaInfo, AssembledPiece piece);
    bool IsFileComplete(InfoHash infoHash, int fileIndex, TorrentMetaInfo metaInfo, Bitfield localBitfield);
    Task FinalizeFileAsync(InfoHash infoHash, int fileIndex, TorrentMetaInfo metaInfo);
}
