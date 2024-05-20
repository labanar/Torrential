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

export interface TorrentAddedEvent {
    infoHash: string;
    totalSize: number;
    name: string;
}


/*
                InfoHash = torrentMetadata.InfoHash,
                AnnounceList = torrentMetadata.AnnounceList,
                TotalSize = torrentMetadata.TotalSize,
                Files = torrentMetadata.Files,
                Name = torrentMetadata.Name,
                NumberOfPieces = torrentMetadata.NumberOfPieces,
                PieceSize = torrentMetadata.PieceSize
                */