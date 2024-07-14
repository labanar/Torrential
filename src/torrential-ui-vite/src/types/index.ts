export interface TorrentSummary {
  infoHash: string;
  name: string;
  bytesDownloaded: number;
  bytesUploaded: number;
  downloadRate: number;
  uploadRate: number;
  sizeInBytes: number;
  progress: number;
  status: string;
}

export interface PeerSummary {
  infoHash: string;
  peerId: string;
  ip: string;
  port: number;
  isSeed: boolean;
}
