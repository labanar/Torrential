using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Torrential.Files;
using Torrential.Torrents;

namespace Torrential.Peers
{
    public class BitfieldManager(TorrentFileService fileService, TorrentEventBus eventBus, TorrentMetadataCache metaCache, TorrentVerificationTracker verificationTracker, ILogger<BitfieldManager> logger)
    {
        private ConcurrentDictionary<InfoHash, Bitfield> _downloadBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _verificationBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _wantedPieceBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _blockBitfields = [];
        private ConcurrentDictionary<InfoHash, Bitfield> _pieceReservationBitfields = [];
        private ConcurrentDictionary<InfoHash, PieceAvailability> _pieceAvailability = [];

        public ICollection<(InfoHash, Bitfield)> DownloadBitfields => _downloadBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();
        public ICollection<(InfoHash, Bitfield)> VerificationBitfields => _verificationBitfields.Select(Bitfield => (Bitfield.Key, Bitfield.Value)).ToArray();

        public void RemoveBitfields(InfoHash infoHash)
        {
            _downloadBitfields.TryRemove(infoHash, out _);
            _verificationBitfields.TryRemove(infoHash, out _);
            _wantedPieceBitfields.TryRemove(infoHash, out _);
            _blockBitfields.TryRemove(infoHash, out _);
            _pieceReservationBitfields.TryRemove(infoHash, out _);
            _pieceAvailability.TryRemove(infoHash, out _);
        }

        public Task Initialize(TorrentMetadata meta, bool hasRecoverableData = false)
            => Initialize(meta, hasRecoverableData ? new RecoverableDataResult { HasRecoverableData = true, PartFileLength = meta.TotalSize, Reason = "legacy" } : null);

        public async Task Initialize(TorrentMetadata meta, RecoverableDataResult? recoveryResult)
        {
            var numPieces = meta.NumberOfPieces;
            var infoHash = meta.InfoHash;

            var downloadBitfield = new Bitfield(numPieces);
            var verificationBitfield = new Bitfield(numPieces);
            var wantedPieceBitfield = BuildWantedPieceBitfield(meta);

            await LoadDownloadBitfieldData(infoHash, downloadBitfield);
            await LoadVerificationBitfieldData(infoHash, verificationBitfield);

            // When we detected a pre-existing part file but have no download bitfield on disk,
            // mark candidate pieces (those covered by the part file's length) as downloaded
            // so they get queued for SHA1 validation.
            if (recoveryResult is { HasRecoverableData: true } && downloadBitfield.HasNone())
            {
                var candidatePieceCount = ComputeCandidatePieceCount(recoveryResult.PartFileLength, meta.PieceSize, numPieces);
                logger.LogInformation("Recovery scan starting for {InfoHash}: part file has {PartFileBytes} bytes, {CandidatePieces}/{TotalPieces} candidate pieces",
                    infoHash, recoveryResult.PartFileLength, candidatePieceCount, numPieces);

                for (var i = 0; i < candidatePieceCount; i++)
                    downloadBitfield.MarkHave(i);
            }

            //We have pieces to download and verify
            if (!verificationBitfield.HasAll(wantedPieceBitfield.Bytes))
            {
                var blockBitfield = new Bitfield(meta.TotalNumberOfChunks);
                var pieceReservationBitfield = new Bitfield(numPieces);
                _pieceReservationBitfields[infoHash] = pieceReservationBitfield;
                _blockBitfields[infoHash] = blockBitfield;
            }

            _pieceAvailability[infoHash] = new PieceAvailability(numPieces);

            _downloadBitfields[infoHash] = downloadBitfield;
            _verificationBitfields[infoHash] = verificationBitfield;
            _wantedPieceBitfields[infoHash] = wantedPieceBitfield;

            //Determine which pieces are downloaded but not verified and request verification
            var piecesToQueueForValidation = new List<int>();
            var skippedAlreadyVerified = 0;
            for (var i = 0; i < numPieces; i++)
            {
                if (!wantedPieceBitfield.HasPiece(i) || !downloadBitfield.HasPiece(i))
                    continue;

                if (verificationBitfield.HasPiece(i))
                {
                    skippedAlreadyVerified++;
                    continue;
                }

                piecesToQueueForValidation.Add(i);
            }

            var queuedCount = piecesToQueueForValidation.Count;
            await verificationTracker.BeginTracking(infoHash, queuedCount);
            foreach (var pieceIndex in piecesToQueueForValidation)
                await eventBus.PublishPieceValidationRequest(new PieceValidationRequest { InfoHash = infoHash, PieceIndex = pieceIndex });

            if (queuedCount > 0 || skippedAlreadyVerified > 0)
            {
                logger.LogInformation("Recovery scan complete for {InfoHash}: {Queued} pieces queued for validation, {Skipped} already verified",
                    infoHash, queuedCount, skippedAlreadyVerified);
            }

            // Recovery add-path: if the part file already contains the full torrent and every wanted piece
            // was already verified from persisted state, validation will not emit completion.
            if (recoveryResult is { HasRecoverableData: true } &&
                recoveryResult.PartFileLength >= meta.TotalSize &&
                queuedCount == 0 &&
                HasAllWantedPieces(infoHash, verificationBitfield))
            {
                await eventBus.PublishTorrentVerificationCompleted(new TorrentVerificationCompletedEvent
                {
                    InfoHash = infoHash,
                    Progress = GetWantedCompletionRatio(infoHash, verificationBitfield)
                });

                logger.LogInformation("Recovered torrent {InfoHash} is already fully verified; publishing completion event", infoHash);
                await eventBus.PublishTorrentComplete(new TorrentCompleteEvent { InfoHash = infoHash });
            }
        }

        /// <summary>
        /// Computes the number of pieces that are at least partially covered by the part file.
        /// A piece is a candidate if the part file contains any bytes for it (even partially).
        /// </summary>
        private static int ComputeCandidatePieceCount(long partFileLength, long pieceSize, int totalPieces)
        {
            if (partFileLength <= 0 || pieceSize <= 0)
                return 0;

            // Number of pieces fully or partially covered by the part file bytes
            var candidateCount = (int)((partFileLength + pieceSize - 1) / pieceSize);
            return Math.Min(candidateCount, totalPieces);
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

        public bool TryGetWantedPieceBitfield(InfoHash infoHash, out Bitfield bitfield)
        {
            return _wantedPieceBitfields.TryGetValue(infoHash, out bitfield);
        }

        public bool HasAllWantedPieces(InfoHash infoHash, Bitfield verificationBitfield)
        {
            if (!_wantedPieceBitfields.TryGetValue(infoHash, out var wantedBitfield))
                return verificationBitfield.HasAll();

            return verificationBitfield.HasAll(wantedBitfield.Bytes);
        }

        public float GetWantedCompletionRatio(InfoHash infoHash, Bitfield verificationBitfield)
        {
            if (!_wantedPieceBitfields.TryGetValue(infoHash, out var wantedBitfield))
                return verificationBitfield.CompletionRatio;

            return verificationBitfield.GetCompletionRatio(wantedBitfield.Bytes);
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

        private static Bitfield BuildWantedPieceBitfield(TorrentMetadata meta)
        {
            var wantedBitfield = new Bitfield(meta.NumberOfPieces);
            foreach (var pieceIndex in meta.WantedPieces)
                wantedBitfield.MarkHave(pieceIndex);

            return wantedBitfield;
        }
    }
}
