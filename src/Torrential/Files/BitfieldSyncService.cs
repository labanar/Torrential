using Microsoft.Extensions.Hosting;
using System.Buffers;
using System.IO.Pipelines;
using Torrential.Peers;

namespace Torrential.Files
{
    internal class BitfieldSyncService(BitfieldManager bitfields, TorrentFileService fileService)
        : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (!stoppingToken.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await FlushDownloadBitfields();
                await FlushVerificationBitfields();
            }
        }


        private async ValueTask FlushDownloadBitfields()
        {
            foreach (var (infoHash, bitfield) in bitfields.DownloadBitfields)
            {
                var path = fileService.GetDownloadBitFieldPath(infoHash);
                using var fs = File.OpenWrite(path);
                var writer = PipeWriter.Create(fs);
                DumpBitfieldToWriter(writer, bitfield);
                await writer.FlushAsync();
                await writer.CompleteAsync();
            }
        }

        private async ValueTask FlushVerificationBitfields()
        {
            foreach (var (infoHash, bitfield) in bitfields.VerificationBitfields)
            {
                var path = fileService.GetVerificationBitFieldPath(infoHash);
                using var fs = File.OpenWrite(path);
                var writer = PipeWriter.Create(fs);
                DumpBitfieldToWriter(writer, bitfield);
                await writer.FlushAsync();
                await writer.CompleteAsync();
            }
        }


        private void DumpBitfieldToWriter(PipeWriter writer, Bitfield bitfield)
        {
            Span<byte> numPiecesBuffer = stackalloc byte[4];
            BitConverterExtensions.TryWriteBigEndian(numPiecesBuffer, bitfield.NumberOfPieces);
            writer.Write(numPiecesBuffer);
            writer.Write(bitfield.Bytes);
        }
    }
}
