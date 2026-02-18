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

export interface ConnectedPeer {
  infoHash: string;
  torrentName: string;
  torrentStatus: string;
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

export interface IntegrationsSettings {
  slackEnabled: boolean;
  slackWebhookUrl: string;
  slackMessageTemplate: string;
  slackTriggerDownloadComplete: boolean;
  discordEnabled: boolean;
  discordWebhookUrl: string;
  discordMessageTemplate: string;
  discordTriggerDownloadComplete: boolean;
  commandHookEnabled: boolean;
  commandTemplate: string;
  commandWorkingDirectory: string | null;
  commandTriggerDownloadComplete: boolean;
}

export type IntegrationsSettingsUpdateRequest = IntegrationsSettings;
