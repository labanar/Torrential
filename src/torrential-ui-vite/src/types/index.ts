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

export interface TorrentDetail {
  infoHash: string;
  name: string;
  status: string;
  progress: number;
  totalSizeBytes: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  downloadRate: number;
  uploadRate: number;
  peers: TorrentDetailPeer[];
  bitfield: BitfieldInfo;
  files: TorrentFile[];
}

export interface TorrentDetailPeer {
  peerId: string;
  ipAddress: string;
  port: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  isSeed: boolean;
  progress: number;
}

export interface BitfieldInfo {
  pieceCount: number;
  haveCount: number;
  bitfield: string; // Base64 encoded
}

export interface TorrentFile {
  id: number;
  filename: string;
  size: number;
  isSelected: boolean;
}
