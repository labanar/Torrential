using System.IO.Pipelines;
using Torrential.Trackers;

namespace Torrential.Peers;

public interface IPeerWireConnection : IDisposable
{
    Guid Id { get; }

    PeerInfo PeerInfo { get; }
    PeerId? PeerId { get; }
    bool IsConnected { get; }
    PipeReader Reader { get; }
    PipeWriter Writer { get; }
    Task<PeerConnectionResult> Connect(InfoHash infoHash, PeerInfo peer, CancellationToken cancellationToken);
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
    public static PeerConnectionResult Failure => new PeerConnectionResult(false);
}
