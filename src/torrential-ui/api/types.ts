export interface TorrentApiModel
{
 infoHash: string;
 name: string;
 progress: number;
 totalSizeBytes: number,
 status: string,  
 peers: PeerApiModel[] 
}

export interface PeerApiModel
{
    peerId :string;
    ipAddress: string;
    port: number;
    bytesDownloaded: number;
    bytesUploaded: number;
    isSeed: boolean;
    progress: number
}