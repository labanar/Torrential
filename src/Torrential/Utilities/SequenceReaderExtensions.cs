using System.Buffers;
using Torrential.Peers;

namespace Torrential.Utilities;

public static class SequenceReaderExtensions
{
    public static bool TryReadInfoHash(this ref SequenceReader<byte> reader, out InfoHash hash)
    {
        hash = InfoHash.None;
        if (!reader.TryReadExact(20, out var infoHashSequence))
            return false;

        var internalSr = new SequenceReader<byte>(infoHashSequence);
        if (!internalSr.TryReadBigEndian(out long p1)) return false;
        if (!internalSr.TryReadBigEndian(out long p2)) return false;
        if (!internalSr.TryReadBigEndian(out int p3)) return false;
        hash = new InfoHash(p1, p2, p3);
        return true;

    }

    public static bool TryReadPeerId(this ref SequenceReader<byte> reader, out PeerId peerId)
    {
        peerId = PeerId.None;
        if (!reader.TryReadExact(20, out var peerIdSequence))
            return false;

        var internalSr = new SequenceReader<byte>(peerIdSequence);
        if (!internalSr.TryReadBigEndian(out long p1)) return false;
        if (!internalSr.TryReadBigEndian(out long p2)) return false;
        if (!internalSr.TryReadBigEndian(out int p3)) return false;
        peerId = new PeerId(p1, p2, p3);
        return true;
    }


    public static bool TryReadPeerExtensions(this ref SequenceReader<byte> reader, out PeerExtensions extensions)
    {
        extensions = PeerExtensions.None;

        if (!reader.TryReadBigEndian(out long extensionsLong))
            return false;

        extensions = PeerExtensions.FromLong(extensionsLong);
        return true;
    }
}
