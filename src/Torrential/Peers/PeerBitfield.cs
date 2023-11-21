using System.Collections;
using System.IO.Pipelines;

namespace Torrential.Peers;
public class PeerBitfield
{
    private readonly int _sizeInBytes;
    private readonly int _numOfPieces;
    private readonly BitArray _bitfield;

    public PeerBitfield(Span<byte> bitfieldData)
    {
        _sizeInBytes = bitfieldData.Length;
        _numOfPieces = _sizeInBytes * 8;
        _bitfield = new BitArray(_numOfPieces);
    }

    public PeerBitfield(int numOfPieces)
    {
        var fullBytes = numOfPieces / 8;
        var remaining = numOfPieces % 8;
        _numOfPieces = numOfPieces;
        _sizeInBytes = remaining == 0 ? fullBytes : fullBytes + 1;
        _bitfield = new BitArray(numOfPieces);
    }

    public bool HasPeice(int index)
    {
        return _bitfield.Get(index);
    }

    public void MarkHave(int index)
    {
        _bitfield.Set(index, true);
    }

    public bool HasAllPieces(int numOfTotalPieces)
    {
        for (int i = 0; i < numOfTotalPieces; i++)
        {
            if (!HasPeice(i))
                return false;
        }
        return true;
    }

    public void WriteToPipeWriter(PipeWriter writer)
    {
        //Write the header and stuff too lol duh
        var buffer = writer.GetSpan(_sizeInBytes + 5);
        buffer.TryWriteBigEndian(_sizeInBytes);
        buffer[4] = PeerWireMessageType.Bitfield;

        for (int i = 0; i < _numOfPieces; i++)
        {
            int byteIndex = i / 8;
            int bitOffset = i % 8;

            if (_bitfield.Get(i))
            {
                buffer[byteIndex + 5] |= (byte)(1 << (7 - bitOffset));
            }
            else
            {
                buffer[byteIndex + 5] &= (byte)~(1 << (7 - bitOffset));
            }
        }

        writer.Advance(_sizeInBytes + 5);
    }
}



public class Bitfield2
{
    public int NumOfPieces => numOfPieces;
    public byte[] Value => bits;

    private byte[] bits;
    private int numOfPieces;
    private readonly int _numOfBytes;


    public Bitfield2(int numOfPieces)
    {
        if (numOfPieces <= 0)
            throw new ArgumentException("Number of pieces must be greater than zero.");

        this.numOfPieces = numOfPieces;
        _numOfBytes = (int)Math.Ceiling(numOfPieces / 8.0);
        this.bits = new byte[_numOfBytes];
    }


    public bool IsPieceAvailable(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= numOfPieces)
            throw new ArgumentOutOfRangeException(nameof(pieceIndex), "Piece index is out of range.");

        int byteIndex = pieceIndex / 8;
        int bitOffset = pieceIndex % 8;
        byte bitmask = (byte)(1 << (7 - bitOffset));
        return (bits[byteIndex] & bitmask) != 0;
    }

    public void SetPieceAvailable(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= numOfPieces)
            throw new ArgumentOutOfRangeException(nameof(pieceIndex), "Piece index is out of range.");

        int byteIndex = pieceIndex / 8;
        int bitOffset = pieceIndex % 8;
        byte bitmask = (byte)(1 << (7 - bitOffset));
        bits[byteIndex] |= bitmask;
    }

    public void SetPieceMissing(int pieceIndex)
    {
        if (pieceIndex < 0 || pieceIndex >= numOfPieces)
            throw new ArgumentOutOfRangeException(nameof(pieceIndex), "Piece index is out of range.");

        int byteIndex = pieceIndex / 8;
        int bitOffset = pieceIndex % 8;
        byte bitmask = (byte)(1 << (7 - bitOffset));
        bits[byteIndex] &= (byte)~bitmask;
    }

    public void LoadDataFromSpan(Span<byte> buffer)
    {
        if (buffer.Length < _numOfBytes)
            throw new ArgumentException("Byte span length must match the size of the bits array.");

        buffer[..Math.Min(buffer.Length, _numOfBytes)].CopyTo(bits);
    }

    public static Bitfield2 FromByteArray(byte[] byteArray, int numOfPieces)
    {
        if (byteArray == null)
            throw new ArgumentNullException(nameof(byteArray));

        if (numOfPieces <= 0)
            throw new ArgumentException("Number of pieces must be greater than zero.");

        int numOfBytes = (int)Math.Ceiling(numOfPieces / 8.0);
        if (byteArray.Length != numOfBytes)
            throw new ArgumentException("Invalid byte array length for the given number of pieces.");

        var bitfield = new Bitfield2(numOfPieces);
        Array.Copy(byteArray, bitfield.bits, numOfBytes);
        return bitfield;
    }

    public void WriteToPipeWriter(PipeWriter writer)
    {
        //Write the header and stuff too lol duh
        var buffer = writer.GetSpan(_numOfBytes + 5);
        buffer.TryWriteBigEndian(_numOfBytes);
        buffer[4] = PeerWireMessageType.Bitfield;
        bits.CopyTo(buffer[5..]);
        writer.Advance(_numOfBytes + 5);
    }
}