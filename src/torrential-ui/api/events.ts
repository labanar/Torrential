export interface PeerConnectedEvent
{
    infoHash: string;
    peerId: string;
    ip: string;
    port: number
}