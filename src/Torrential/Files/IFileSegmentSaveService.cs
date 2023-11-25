using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Channels;
using Torrential.Peers;
using Torrential.Torrents;
using Torrential.Utilities;

namespace Torrential.Files
{
    public interface IFileSegmentSaveService
    {
        void SaveSegment(PooledPieceSegment segment);
    }


    public class FileSegmentSaveService(IFileHandleProvider fileHandleProvider, TorrentMetadataCache metaCache, ILogger<FileSegmentSaveService> logger, BitfieldManager bitfieldManager)
        : IFileSegmentSaveService
    {
        private static readonly int SEGMENT_LENGTH = (int)Math.Pow(2, 14);
        private ConcurrentDictionary<InfoHash, Bitfield> _segmentFields = [];

        public void SaveSegment(PooledPieceSegment segment)
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


                var fileHandle = fileHandleProvider.GetFileHandle(segment.InfoHash);

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
                    PieceValidator.REQUEST_CHANNEL.TryWrite(new() { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
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

    //public class PieceSaveService(ILogger<PieceSaveService> logger, TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, BitfieldManager bitfieldManager)
    //    : BackgroundService
    //{
    //    private static Channel<PooledPieceSegment> SEGMENT_CHANNEL = Channel.CreateUnbounded<PooledPieceSegment>(new UnboundedChannelOptions
    //    {
    //        SingleWriter = false,
    //        SingleReader = true
    //    });

    //    public static ChannelWriter<PooledPieceSegment> SAVE_CHANNEL = SEGMENT_CHANNEL.Writer;

    //    private ConcurrentDictionary<InfoHash, Bitfield> _segmentFields = [];
    //    private static readonly int SEGMENT_LENGTH = (int)Math.Pow(2, 14);

    //    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //    {
    //        await foreach (var segment in SEGMENT_CHANNEL.Reader.ReadAllAsync(stoppingToken))
    //        {
    //            using (segment)
    //            {
    //                if (!metaCache.TryGet(segment.InfoHash, out var meta))
    //                    continue;

    //                //Have we already saved this piece?
    //                if (!bitfieldManager.TryGetBitfield(segment.InfoHash, out var bitfield))
    //                    continue;

    //                if (bitfield.HasPiece(segment.PieceIndex))
    //                {
    //                    logger.LogInformation("Already verified {Piece} - ignoring segment data!", segment.PieceIndex);
    //                    continue;
    //                }


    //                var pieceSize = meta.PieceSize;
    //                var numSegmentsPerPiece = (int)(pieceSize / SEGMENT_LENGTH);
    //                var segmentField = _segmentFields.GetOrAdd(segment.InfoHash, (_) =>
    //                {
    //                    return new Bitfield(numSegmentsPerPiece * meta.NumberOfPieces);
    //                });


    //                var fileHandle = fileHandleProvider.GetFileHandle(segment.InfoHash);

    //                long fileOffset = (segment.PieceIndex * meta.PieceSize) + segment.Offset;
    //                RandomAccess.Write(fileHandle, segment.Buffer, fileOffset);

    //                var remainder = pieceSize % SEGMENT_LENGTH;
    //                if (remainder > 0)
    //                    numSegmentsPerPiece += 1;


    //                var segmentFieldIndex = segment.PieceIndex * numSegmentsPerPiece;
    //                var offsetIdx = segment.Offset / SEGMENT_LENGTH;
    //                segmentFieldIndex += offsetIdx;
    //                await segmentField.MarkHaveAsync(segmentFieldIndex, stoppingToken);


    //                if (HasAllSegmentsForPiece(segment.InfoHash, segment.PieceIndex, numSegmentsPerPiece))
    //                {
    //                    PieceValidator.REQUEST_CHANNEL.TryWrite(new() { InfoHash = segment.InfoHash, PieceIndex = segment.PieceIndex });
    //                }
    //            }
    //        }
    //    }

    //    public bool HasAllSegmentsForPiece(InfoHash infoHah, int pieceIndex, int segmentsPerPiece)
    //    {
    //        if (!_segmentFields.TryGetValue(infoHah, out var segmentField))
    //            return false;

    //        var segmentIndex = pieceIndex * segmentsPerPiece;
    //        for (int i = 0; i < segmentsPerPiece; i++)
    //        {
    //            if (!segmentField.HasPiece(segmentIndex + i)) return false;
    //        }

    //        return true;
    //    }
    //}


    public class PieceValidator(ILogger<PieceValidator> logger, TorrentMetadataCache metaCache, IFileHandleProvider fileHandleProvider, BitfieldManager bitfieldMgr)
        : BackgroundService
    {
        private static Channel<PieceValidationRequest> VALIDATION_CHANNEL = Channel.CreateUnbounded<PieceValidationRequest>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        public static ChannelWriter<PieceValidationRequest> REQUEST_CHANNEL = VALIDATION_CHANNEL.Writer;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var request in VALIDATION_CHANNEL.Reader.ReadAllAsync(stoppingToken))
            {
                //Have we already verified this piece?
                if (!bitfieldMgr.TryGetBitfield(request.InfoHash, out var bitfield))
                    continue;

                if (bitfield.HasPiece(request.PieceIndex))
                {
                    logger.LogInformation("Already verified piece {Piece} - ignoring", request.PieceIndex);
                    continue;
                }


                if (!metaCache.TryGet(request.InfoHash, out var meta))
                {
                    logger.LogError("Could not find torrent metadata");
                    continue;
                }

                var fileHandle = fileHandleProvider.GetFileHandle(request.InfoHash);
                var buffer = ArrayPool<byte>.Shared.Rent(20);
                meta.GetPieceHash(request.PieceIndex).CopyTo(buffer);

                var pipe = new Pipe();
                var fillTask = FillPipeWithPiece(fileHandle, pipe.Writer, request.PieceIndex, (int)meta.PieceSize).ConfigureAwait(true);
                var result = await Sha1Helper.VerifyHash(pipe.Reader, buffer, Sha1Helper.CHUNK_SIZE);
                ArrayPool<byte>.Shared.Return(buffer);
                await fillTask;
                logger.LogInformation("Validation result for {Piece}: {Result}", request.PieceIndex, result);

                if (result)
                {
                    bitfield.MarkHave(request.PieceIndex);
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
