using MassTransit;
using System.Collections.Concurrent;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Peers
{
    public class BitfieldManager(TorrentFileService fileService, IBus bus, TorrentMetadataCache metaCache)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _downloadBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _verificationBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _blockBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _pieceReservationBitfields = [];
        private ConcurrentDictionary<InfoHash, PieceAvailability> _pieceAvailability = [];

        public ICollection<(InfoHash, Bitfield)> DownloadBitfields => _downloadBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();
        public ICollection<(InfoHash, Bitfield)> VerificationBitfields => _verificationBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();

        public void RemoveBitfields(InfoHash infoHash)
        {
            _downloadBitfields.TryRemove(infoHash, out _);
            _verificationBitfields.TryRemove(infoHash, out _);
            _blockBitfields.TryRemove(infoHash, out _);
            _pieceReservationBitfields.TryRemove(infoHash, out _);
            _pieceAvailability.TryRemove(infoHash, out _);
        }

        public async Task Initialize(TorrentMetadata meta)
        {
            var numPieces = meta.NumberOfPieces;
            var infoHash = meta.InfoHash;

            var downloadBitfield = new Bitfield(numPieces);
            var verificationBitfield = new Bitfield(numPieces);


            await LoadDownloadBitfieldData(infoHash, downloadBitfield);
            await LoadVerificationBitfieldData(infoHash, verificationBitfield);

            //We have pieces to download and verify
            if (!verificationBitfield.HasAll())
            {
                var blockBitfield = new Bitfield(meta.TotalNumberOfChunks);
                var pieceReservationBitfield = new Bitfield(numPieces);
                _pieceReservationBitfields[infoHash] = pieceReservationBitfield;
                _blockBitfields[infoHash] = blockBitfield;
            }

            _pieceAvailability[infoHash] = new PieceAvailability(numPieces);

            _downloadBitfields[infoHash] = downloadBitfield;
            _verificationBitfields[infoHash] = verificationBitfield;

            //Determine which pieces are downloaded but not verified and request verification
            for (var i = 0; i < numPieces; i++)
            {
                if (downloadBitfield.HasPiece(i) && !verificationBitfield.HasPiece(i))
                    await bus.Publish(new PieceValidationRequest { InfoHash = infoHash, PieceIndex = i });
            }
        }

        public bool TryGetBlockBitfield(InfoHash infoHash, out Bitfield? bitfield)
        {
            return _blockBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetDownloadBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _downloadBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetVerificationBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _verificationBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetPieceReservationBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _pieceReservationBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetPieceAvailability(InfoHash infoHash, out PieceAvailability? availability)
        {
            return _pieceAvailability.TryGetValue(infoHash, out availability);
        }


        private async ValueTask LoadDownloadBitfieldData(InfoHash infoHash, Bitfield bitField)
        {
            var path = await fileService.GetDownloadBitFieldPath(infoHash);
            if (!File.Exists(path))
                return;

            using var fs = File.OpenRead(path);
            fs.Seek(4, SeekOrigin.Begin);

            var buffer = new byte[bitField.Bytes.Length];
            fs.ReadExactly(buffer);
            bitField.Fill(buffer);
        }

        private async ValueTask LoadVerificationBitfieldData(InfoHash infoHash, Bitfield bitField)
        {
            var path = await fileService.GetVerificationBitFieldPath(infoHash);
            if (!File.Exists(path))
                return;

            using var fs = File.OpenRead(path);
            fs.Seek(4, SeekOrigin.Begin);

            var buffer = new byte[bitField.Bytes.Length];
            fs.ReadExactly(buffer);
            bitField.Fill(buffer);
        }
    }
}
