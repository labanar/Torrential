namespace Torrential.Core;

public sealed class PeerWireState
{
    public DateTimeOffset LastChokedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool AmChoked { get; set; } = true;
    public bool PeerChoked { get; set; } = true;
    public bool AmInterested { get; set; } = false;
    public bool PeerInterested { get; set; } = false;
    public Bitfield? PeerBitfield { get; set; } = null;
    public DateTimeOffset PeerLastInterestedAt { get; set; } = DateTimeOffset.UtcNow;
}
