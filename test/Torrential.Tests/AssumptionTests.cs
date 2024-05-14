using System.IO.Pipelines;
using System.Threading.Channels;

namespace Torrential.Tests;

public class AssumptionTests
{



    [Fact]
    public async Task Wait_to_write_returns_throws_when_cancelled()
    {
        var sut = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false
        });


        //Let's put one item in the channel to block the writer from subsequent writes
        await sut.Writer.WriteAsync(1);

        //Now we'll try to wait to write, but we'll cancel the token after 5s
        var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        //No we wait to write, this should fail by throwing 
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await sut.Writer.WaitToWriteAsync(cts.Token);
        });
    }


    [Fact]
    public async Task Try_write_fails_when_channel_full()
    {
        var sut = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false
        });


        //Let's put one item in the channel to block the writer from subsequent writes
        await sut.Writer.WriteAsync(1);

        var writeResult = sut.Writer.TryWrite(2);
        Assert.False(writeResult);
    }


    [Fact]
    public async Task PipeReader_with_null_stream_completes_immediately()
    {
        var sut = PipeReader.Create(Stream.Null);
        var result = await sut.ReadAsync();
        Assert.True(result.IsCompleted);
    }

    [Fact]
    public async Task PipeWriter_with_null_stream_does_not_complete_immediately()
    {
        var sut = PipeWriter.Create(Stream.Null);
        var result = await sut.WriteAsync(new ReadOnlyMemory<byte>(new byte[] { 1, 2, 3, 4, 5 }));
        Assert.False(result.IsCompleted);
    }
}
