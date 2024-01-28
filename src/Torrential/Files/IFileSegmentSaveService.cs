using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using Torrential.Peers;
using Torrential.Torrents;
using Torrential.Utilities;

namespace Torrential.Files
{
    public interface IFileSegmentSaveService
    {
        Task SaveSegment(PooledPieceSegment segment);
    }

    public class FileSegmentSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache, ILogger<FileSegmentSaveService> logger, BitfieldManager bitfieldManager, IBus bus)
        : IFileSegmentSaveService
    {
        private static readonly int SEGMENT_LENGTH = (int)Math.Pow(2, 14);
        private ConcurrentDictionary<InfoHash, Bitfield> _segmentFields = [];

        public async Task SaveSegment(PooledPieceSegment segment)
        {
            using (segment)
            {
                if (!metaCache.TryGet(segment.InfoHash, out var meta))
                    return;

                //Have we already saved this piece?
                if (!bitfieldManager.TryGetBitfield(segment.InfoHash, out var bitfield))
                    return;

                if (bitfield.HasPiece(segment.PieceIndex))
                {
                    logger.LogInformation("Already verified {Piece} - ignoring segment data!", segment.PieceIndex);
                    return;
                }

                var pieceSize = meta.PieceSize;
                var numSegmentsPerPiece = (int)(pieceSize / SEGMENT_LENGTH);
                var segmentField = _segmentFields.GetOrAdd(segment.InfoHash, (_) =>
                {
                    return new Bitfield(numSegmentsPerPiece * meta.NumberOfPieces);
                });


                var fileHandle = fileHandleProvider.GetPartFileHandle(segment.InfoHash);

                long fileOffset = (segment.PieceIndex * meta.PieceSize) + segment.Offset;
                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);

                var remainder = pieceSize % SEGMENT_LENGTH;
                if (remainder > 0)
                    numSegmentsPerPiece += 1;

                var segmentFieldIndex = segment.PieceIndex * numSegmentsPerPiece;
                var offsetIdx = segment.Offset / SEGMENT_LENGTH;
                segmentFieldIndex += offsetIdx;
                segmentField.MarkHave(segmentFieldIndex);
                if (HasAllSegmentsForPiece(segment.InfoHash, segment.PieceIndex, numSegmentsPerPiece))
                {
                    await bus.Publish(new PieceValidationRequest { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                    await bus.Publish(new TorrentPieceDownloadedEvent { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
                }
            }
        }

        public bool HasAllSegmentsForPiece(InfoHash infoHah, int pieceIndex, int segmentsPerPiece)
        {
            if (!_segmentFields.TryGetValue(infoHah, out var segmentField))
                return false;

            var segmentIndex = pieceIndex * segmentsPerPiece;
            for (int i = 0; i < segmentsPerPiece; i++)
            {
                if (!segmentField.HasPiece(segmentIndex + i)) return false;
            }

            return true;
        }
    }

    public sealed class PieceSegment
        : IAsyncDisposable, IDisposable
    {
        private readonly InfoHash _hash;
        private readonly byte[] _buffer;
        private readonly int _index;
        private readonly int _offset;
        private readonly int _length;
        public InfoHash InfoHash => _hash;
        public int Index => _index;
        public int Offset => _offset;
        public ReadOnlySpan<byte> Payload => _buffer.AsSpan()[.._length];

        public PieceSegment(InfoHash infoHash, int index, int offset, ReadOnlySpan<byte> payload)
        {
            _hash = infoHash;
            _buffer = ArrayPool<byte>.Shared.Rent(payload.Length);
            _index = index;
            _offset = offset;
            _length = payload.Length;
            payload.CopyTo(_buffer);
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }

        public ValueTask DisposeAsync()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            return ValueTask.CompletedTask;
        }
    }

    public class PieceValidationRequest
    {
        public required InfoHash InfoHash { get; init; }
        public required int PieceIndex { get; init; }
    }


    public class PieceValidator(ILogger<PieceValidator> logger, TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, BitfieldManager bitfieldMgr, IBus bus)
        : IConsumer<PieceValidationRequest>
    {
        public async Task Consume(ConsumeContext<PieceValidationRequest> context)
        {
            var request = context.Message;

            //Have we already verified this piece?
            if (!bitfieldMgr.TryGetBitfield(request.InfoHash, out var bitfield))
                return;

            if (bitfield.HasPiece(request.PieceIndex))
                logger.LogInformation("Already verified piece {Piece} - ignoring", request.PieceIndex);


            if (!metaCache.TryGet(request.InfoHash, out var meta))
                logger.LogError("Could not find torrent metadata");

            var fileHandle = fileHandleProvider.GetPartFileHandle(request.InfoHash);
            var buffer = ArrayPool<byte>.Shared.Rent(20);
            meta.GetPieceHash(request.PieceIndex).CopyTo(buffer);

            var pipe = new System.IO.Pipelines.Pipe();
            var fillTask = FillPipeWithPiece(fileHandle, pipe.Writer, request.PieceIndex, (int)meta.PieceSize).ConfigureAwait(true);
            var result = await Sha1Helper.VerifyHash(pipe.Reader, buffer, Sha1Helper.CHUNK_SIZE);
            ArrayPool<byte>.Shared.Return(buffer);
            await fillTask;
            logger.LogInformation("Validation result for {Piece}: {Result}", request.PieceIndex, result);

            if (result)
            {
                bitfield.MarkHave(request.PieceIndex);
                await bus.Publish(new TorrentPieceVerifiedEvent { InfoHash = request.InfoHash, PieceIndex = request.PieceIndex });
                if (bitfield.HasAll())
                {
                    logger.LogInformation("ALL PIECES ACQUIRED!");
                    await bus.Publish(new TorrentCompleteEvent { InfoHash = request.InfoHash });
                }
            }
        }

        private async Task FillPipeWithPiece(SafeFileHandle sfh, PipeWriter writer, int pieceIndex, int pieceSize)
        {
            var mem = writer.GetMemory(pieceSize);
            var offset = 1L * pieceIndex * pieceSize;
            var bytesRead = await RandomAccess.ReadAsync(sfh, mem, offset);
            writer.Advance(bytesRead);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }
    }
}
