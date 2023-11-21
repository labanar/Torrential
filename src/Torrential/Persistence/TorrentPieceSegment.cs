using System.Collections;

namespace Torrential.Persistence
{
    internal struct TorrentPieceSegment
    {
        public int PieceIndex { get; init; }
        public int Offset { get; init; }
        public bool IsDownloaded { get; set; }
        public int Length { get; init; }
    }

    internal struct TorrentPiece
    {
        public required int Index { get; init; }
        public required int Size { get; init; }
        public required InfoHash InfoHash { get; init; }
        public bool IsHashVerified { get; init; }
        public BitArray SegmentsDownloaded { get; init; }
    }
}
