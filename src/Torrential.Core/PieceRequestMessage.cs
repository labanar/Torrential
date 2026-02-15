using System.Buffers;

namespace Torrential.Core;

public readonly record struct PieceRequestMessage(int PieceIndex, int Begin, int Length)
{
    public static PieceRequestMessage FromReadOnlySequence(ReadOnlySequence<byte> payload)
    {
        var reader = new SequenceReader<byte>(payload);
        if (!reader.TryReadBigEndian(out int pieceIndex))
            throw new InvalidDataException("Could not read piece index");
        if (!reader.TryReadBigEndian(out int begin))
            throw new InvalidDataException("Could not read begin index");
        if (!reader.TryReadBigEndian(out int length))
            throw new InvalidDataException("Could not read length");

        return new PieceRequestMessage(pieceIndex, begin, length);
    }
}
