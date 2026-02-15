export interface PeerSummary {
  peerId: string;
  ipAddress: string;
  port: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  isSeed: boolean;
  progress: number;
}

export interface TorrentState {
  infoHash: string;
  name: string;
  progress: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  downloadRate: number;
  uploadRate: number;
  totalSizeBytes: number;
  peers: PeerSummary[];
  status: 'Running' | 'Stopped' | 'Idle';
}
