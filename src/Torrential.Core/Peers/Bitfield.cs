using System.Buffers;
using System.Numerics;

namespace Torrential.Core.Peers;

public sealed class Bitfield : IBitfield, IDisposable
{
    private readonly int _numOfPieces;
    private readonly int _sizeInBytes;
    private readonly byte[] _bitfield;

    public int NumberOfPieces => _numOfPieces;
    public ReadOnlySpan<byte> Bytes => _bitfield.AsSpan().Slice(0, _sizeInBytes);

    public float CompletionRatio
    {
        get
        {
            int piecesHave = 0;
            for (int i = 0; i < _sizeInBytes; i++)
                piecesHave += BitOperations.PopCount(_bitfield[i]);
            return piecesHave / (float)_numOfPieces;
        }
    }

    public Bitfield(int numOfPieces)
    {
        _numOfPieces = numOfPieces;
        _sizeInBytes = (numOfPieces + 7) / 8;
        _bitfield = ArrayPool<byte>.Shared.Rent(_sizeInBytes);
        _bitfield.AsSpan(0, _sizeInBytes).Clear();
    }

    public Bitfield(Span<byte> data)
    {
        _sizeInBytes = data.Length;
        _numOfPieces = _sizeInBytes * 8;
        _bitfield = ArrayPool<byte>.Shared.Rent(_sizeInBytes);
        data.CopyTo(_bitfield);
    }

    public Bitfield(ReadOnlySequence<byte> data)
    {
        _sizeInBytes = (int)data.Length;
        _numOfPieces = _sizeInBytes * 8;
        _bitfield = ArrayPool<byte>.Shared.Rent(_sizeInBytes);
        data.CopyTo(_bitfield);
    }

    public void Fill(Span<byte> data)
    {
        data.CopyTo(_bitfield);
    }

    public bool HasAll()
    {
        int fullBytes = _numOfPieces / 8;
        for (int i = 0; i < fullBytes; i++)
        {
            if (_bitfield[i] != 0xFF)
                return false;
        }

        int remainingBits = _numOfPieces % 8;
        if (remainingBits > 0)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((_bitfield[fullBytes] & mask) != mask)
                return false;
        }

        return true;
    }

    public bool HasNone()
    {
        for (int i = 0; i < _sizeInBytes; i++)
        {
            if (_bitfield[i] != 0)
                return false;
        }

        return true;
    }

    public bool HasPiece(int index)
    {
        if (index < 0 || index >= _numOfPieces)
            throw new ArgumentOutOfRangeException(nameof(index));

        var byteIndex = index / 8;
        var bitIndex = 7 - (index % 8);
        return (_bitfield[byteIndex] & (1 << bitIndex)) != 0;
    }

    /// <summary>
    /// WARNING: This method is not thread-safe, and should only be called in a single-threaded context.
    /// </summary>
    public void MarkHave(int index)
    {
        if (index < 0 || index >= _numOfPieces)
            throw new ArgumentOutOfRangeException(nameof(index));

        var byteIndex = index / 8;
        var bitIndex = 7 - (index % 8);
        _bitfield[byteIndex] |= (byte)(1 << bitIndex);
    }

    public void Dispose()
    {
        if (_bitfield != null)
            ArrayPool<byte>.Shared.Return(_bitfield, true);
    }
}
