using System.Buffers;
using System.Collections.Concurrent;
using Torrential.Files;

namespace Torrential.Peers
{
    public class BitfieldManager(TorrentFileService fileService)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _downloadBitfields = new ConcurrentDictionary<InfoHash, Bitfield>();
        private ConcurrentDictionary<InfoHash, Bitfield> _verificationBitfields = new ConcurrentDictionary<InfoHash, Bitfield>();

        public ICollection<(InfoHash, Bitfield)> DownloadBitfields => _downloadBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();
        public ICollection<(InfoHash, Bitfield)> VerificationBitfields => _verificationBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();

        public void Initialize(InfoHash infoHash, int numberOfPieces)
        {
            var downloadBitfield = new Bitfield(numberOfPieces);
            var verificationBitfield = new Bitfield(numberOfPieces);

            LoadDownloadBitfieldData(infoHash, downloadBitfield);
            LoadVerificationBitfieldData(infoHash, verificationBitfield);

            _downloadBitfields[infoHash] = downloadBitfield;
            _verificationBitfields[infoHash] = verificationBitfield;

        }

        public bool TryGetDownloadBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _downloadBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool TryGetVerificationBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _verificationBitfields.TryGetValue(infoHash, out bitfield);
        }


        private void LoadDownloadBitfieldData(InfoHash infoHash, Bitfield bitField)
        {
            var path = fileService.GetDownloadBitFieldPath(infoHash);
            if (!File.Exists(path))
                return;

            using var fs = File.OpenRead(path);
            fs.Seek(4, SeekOrigin.Begin);

            Span<byte> buffer = stackalloc byte[bitField.Bytes.Length];
            fs.ReadExactly(buffer);
            bitField.Fill(buffer);
        }

        private void LoadVerificationBitfieldData(InfoHash infoHash, Bitfield bitField)
        {
            var path = fileService.GetVerificationBitFieldPath(infoHash);
            if (!File.Exists(path))
                return;

            using var fs = File.OpenRead(path);
            fs.Seek(4, SeekOrigin.Begin);

            Span<byte> buffer = stackalloc byte[bitField.Bytes.Length];
            fs.ReadExactly(buffer);
            bitField.Fill(buffer);
        }
    }
}
