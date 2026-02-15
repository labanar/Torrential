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

export interface TorrentDetails {
  infoHash: string;
  name: string;
  totalSizeBytes: number;
  pieceSize: number;
  numberOfPieces: number;
  status: 'Running' | 'Stopped' | 'Idle';
  dateAdded: string;
  pieces: boolean[];
  files: TorrentFileDetail[];
  peers: PeerDetail[];
}

export interface TorrentFileDetail {
  fileIndex: number;
  fileName: string;
  fileSize: number;
  selected: boolean;
}

export interface PeerDetail {
  peerId: string;
  ipAddress: string;
  port: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  isSeed: boolean;
  progress: number;
  pieces: boolean[];
}

export interface Settings {
  id: number;
  downloadFolder: string;
  completedFolder: string;
  maxHalfOpenConnections: number;
  maxPeersPerTorrent: number;
}
