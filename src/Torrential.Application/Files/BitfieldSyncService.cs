using Microsoft.Extensions.Hosting;
using System.IO.Pipelines;
using Torrential.Application.Peers;

namespace Torrential.Application.Files;

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
            var path = await fileService.GetDownloadBitFieldPath(infoHash);
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
            var path = await fileService.GetVerificationBitFieldPath(infoHash);
            using var fs = File.OpenWrite(path);
            var writer = PipeWriter.Create(fs);
            DumpBitfieldToWriter(writer, bitfield);
            await writer.FlushAsync();
            await writer.CompleteAsync();
        }
    }


    private void DumpBitfieldToWriter(PipeWriter writer, IBitfield bitfield)
    {
        Span<byte> numPiecesBuffer = stackalloc byte[4];
        BitConverterExtensions.TryWriteBigEndian(numPiecesBuffer, bitfield.NumberOfPieces);

        var span = writer.GetSpan(4);
        numPiecesBuffer.CopyTo(span);
        writer.Advance(4);

        var bytes = bitfield.Bytes;
        var byteSpan = writer.GetSpan(bytes.Length);
        bytes.CopyTo(byteSpan);
        writer.Advance(bytes.Length);
    }
}
