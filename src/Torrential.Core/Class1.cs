using System.Buffers;

namespace Torrential.Core;


//Size of a piece is dynamic per torrent, however chunk size is generally 16KB
//So we can allocate a 16KB buffer for each peer
//This scales well with the number of peers we have
//100 peers = 1.6MB of memory
//1000 peers = 16MB of memory
//10000 peers = 160MB of memory
//100000 peers = 1.6GB of memory

//Array pooling could be used to reduce memory usage too
//Only if a peer is actively downloading or uploading data, then we can rent a buffer from the pool
//When we are done with the upload, we return the buffer back to the pool

public class PeerState
{
    //Keep the buffer for the current piece of data we're downloading from the peers


}


public class Chunk : IDisposable
{
    public int Size { get; }

    private readonly byte[] _buffer;

    public Chunk(int size)
    {
        Size = size;
        _buffer = ArrayPool<byte>.Shared.Rent(Size);
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
    }
}