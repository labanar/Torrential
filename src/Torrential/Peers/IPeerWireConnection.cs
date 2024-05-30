using System.IO.Pipelines;
using Torrential.Trackers;

namespace Torrential.Peers;

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

public readonly struct PeerConnectionResult
{
    public readonly bool Success;
    private readonly PeerId PeerId;
    private readonly PeerExtensions Extensions;
    private PeerConnectionResult(PeerId peerId, PeerExtensions extensions)
    {
        Success = true;
        PeerId = peerId;
        Extensions = extensions;
    }

    private PeerConnectionResult(bool success)
    {
        Success = success;
    }

    internal static PeerConnectionResult FromHandshake(HandshakeResponse handshake) => !handshake.Success ? Failure : new PeerConnectionResult(handshake.PeerId, handshake.Extensions);
    public static PeerConnectionResult Failure { get; } = new PeerConnectionResult(false);
}
