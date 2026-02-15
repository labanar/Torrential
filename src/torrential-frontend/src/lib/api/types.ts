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

export interface ParsedTorrentFile {
  fileIndex: number;
  fileName: string;
  fileSize: number;
}

export interface ParsedTorrent {
  infoHash: string;
  name: string;
  totalSize: number;
  pieceSize: number;
  numberOfPieces: number;
  files: ParsedTorrentFile[];
  announceUrls: string[];
  pieceHashes: string;
}
