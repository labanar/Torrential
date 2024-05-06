export interface PeerConnectedEvent
{
    infoHash: string;
    peerId: string;
    ip: string;
    port: number;
}

export interface PeerBitfieldReceivedEvent
{
    hasAllPieces: boolean;
    peerId: string;
    infoHash: string;
}