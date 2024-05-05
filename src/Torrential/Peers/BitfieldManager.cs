using MassTransit;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;

namespace Torrential.Peers
{
    public class BitfieldManager(TorrentFileService fileService, IBus bus)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _downloadBitfields = new ConcurrentDictionary<InfoHash, Bitfield>();
        private ConcurrentDictionary<InfoHash, Bitfield> _verificationBitfields = new ConcurrentDictionary<InfoHash, Bitfield>();

        public ICollection<(InfoHash, Bitfield)> DownloadBitfields => _downloadBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();
        public ICollection<(InfoHash, Bitfield)> VerificationBitfields => _verificationBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();

        public async Task Initialize(InfoHash infoHash, int numberOfPieces)
        {
            var downloadBitfield = new Bitfield(numberOfPieces);
            var verificationBitfield = new Bitfield(numberOfPieces);

            await LoadDownloadBitfieldData(infoHash, downloadBitfield);
            await LoadVerificationBitfieldData(infoHash, verificationBitfield);

            _downloadBitfields[infoHash] = downloadBitfield;
            _verificationBitfields[infoHash] = verificationBitfield;

            //Determine which pieces are downloaded but not verified
            for (var i = 0; i < numberOfPieces; i++)
            {
                if (downloadBitfield.HasPiece(i) && !verificationBitfield.HasPiece(i))
                    await bus.Publish(new PieceValidationRequest { InfoHash = infoHash, PieceIndex = i });
            }
        }

        public bool TryGetDownloadBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _downloadBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetVerificationBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _verificationBitfields.TryGetValue(infoHash, out bitfield);
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
