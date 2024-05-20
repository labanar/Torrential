export interface PeerConnectedEvent {
    infoHash: string;
    peerId: string;
    ip: string;
    port: number;
}

export interface TorrentStatsEvent
{
    infoHash: string;
    uploadRate: number;
    downloadRate: number;
}

export interface PeerDisconnectedEvent {
    infoHash: string;
    peerId: string;
}

export interface PeerBitfieldReceivedEvent {
    hasAllPieces: boolean;
    peerId: string;
    infoHash: string;
}

export interface PieceVerifiedEvent {
    infoHash: string;
    pieceIndex: number;
    progress: number;
}

export interface TorrentStartedEvent {
    infoHash: string;
}

export interface TorrentStoppedEvent {
    infoHash: string
}

export interface TorrentRemovedEvent {
    infoHash: string
}