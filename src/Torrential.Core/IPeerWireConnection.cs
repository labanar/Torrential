using System.IO.Pipelines;

namespace Torrential.Core;

public interface IPeerWireConnection : IAsyncDisposable
{
    Guid Id { get; }
    PeerInfo PeerInfo { get; }
    PeerId? PeerId { get; }
    PipeReader Reader { get; }
    PipeWriter Writer { get; }
    InfoHash InfoHash { get; }
    void SetInfoHash(InfoHash infoHash);
    void SetPeerId(PeerId peerId);
    DateTimeOffset ConnectionTimestamp { get; }
}
