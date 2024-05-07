export interface TorrentSummary {
  infoHash: string;
  name: string;
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
