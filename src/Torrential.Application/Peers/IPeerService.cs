namespace Torrential.Application.Peers;

public interface IPeerService
{
    Peer Self { get; }
}

public readonly struct Peer
{
    public readonly PeerId Id;

    public Peer(PeerId id)
    {
        Id = id;
    }
}


public sealed class PeerService : IPeerService
{
    public Peer Self { get; }

    public PeerService()
    {
        Self = new Peer(PeerId.New);
    }
}
