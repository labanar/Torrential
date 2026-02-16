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

export interface TorrentPreviewFileSummary {
  id: number;
  filename: string;
  sizeBytes: number;
}

export interface TorrentPreviewSummary {
  name: string;
  infoHash: string;
  totalSizeBytes: number;
  files: TorrentPreviewFileSummary[];
}
