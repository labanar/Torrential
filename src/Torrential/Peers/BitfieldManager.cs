using MassTransit;
using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;

namespace Torrential.Peers
{
    public class BitfieldManager(TorrentFileService fileService, IBus bus)
    {
        private ConcurrentDictionary<InfoHash, AsyncBitfield> _downloadBitfields = new ConcurrentDictionary<InfoHash, AsyncBitfield>();
        private ConcurrentDictionary<InfoHash, AsyncBitfield> _verificationBitfields = new ConcurrentDictionary<InfoHash, AsyncBitfield>();

        public ICollection<(InfoHash, AsyncBitfield)> DownloadBitfields => _downloadBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();
        public ICollection<(InfoHash, AsyncBitfield)> VerificationBitfields => _verificationBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();

        public async Task Initialize(InfoHash infoHash, int numberOfPieces)
        {
            var downloadBitfield = new AsyncBitfield(numberOfPieces);
            var verificationBitfield = new AsyncBitfield(numberOfPieces);

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

        public bool TryGetDownloadBitfield(InfoHash infoHash, out AsyncBitfield bitfield)
        {
            return _downloadBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetVerificationBitfield(InfoHash infoHash, out AsyncBitfield bitfield)
        {
            return _verificationBitfields.TryGetValue(infoHash, out bitfield);
        }


        private async ValueTask LoadDownloadBitfieldData(InfoHash infoHash, AsyncBitfield bitField)
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

        private async ValueTask LoadVerificationBitfieldData(InfoHash infoHash, AsyncBitfield bitField)
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
