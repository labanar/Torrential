export function fetchTorrents() {}

interface ApiErrorData {
  code: string;
}

interface ApiResponse<T> {
  data?: T;
  error?: ApiErrorData;
}

export interface TorrentApiModel {
  infoHash: string;
  name: string;
  progress: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  downloadRate: number;
  uploadRate: number;
  totalSizeBytes: number;
  status: string;
  peers: PeerApiModel[];
}

export interface PeerApiModel {
  peerId: string;
  ipAddress: string;
  port: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  isSeed: boolean;
  progress: number;
}

export interface PeerConnectedEvent {
  infoHash: string;
  peerId: string;
  ip: string;
  port: number;
}

export interface TorrentStatsEvent {
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
  verifiedPieces?: number[];
}

export interface FileSelectionChangedEvent {
  infoHash: string;
  selectedFileIds: number[];
}

export interface TorrentStartedEvent {
  infoHash: string;
}

export interface TorrentStoppedEvent {
  infoHash: string;
}

export interface TorrentCompletedEvent {
  infoHash: string;
}

export interface TorrentRemovedEvent {
  infoHash: string;
}

export interface TorrentAddedEvent {
  infoHash: string;
  totalSize: number;
  name: string;
}

export interface TorrentDetailApiModel {
  infoHash: string;
  name: string;
  status: string;
  progress: number;
  totalSizeBytes: number;
  bytesDownloaded: number;
  bytesUploaded: number;
  downloadRate: number;
  uploadRate: number;
  peers: PeerApiModel[];
  bitfield: BitfieldApiModel;
  files: TorrentFileApiModel[];
}

export interface BitfieldApiModel {
  pieceCount: number;
  haveCount: number;
  bitfield: string;
}

export interface TorrentFileApiModel {
  id: number;
  filename: string;
  size: number;
  isSelected: boolean;
}

export async function fetchTorrentDetail(infoHash: string): Promise<TorrentDetailApiModel | null> {
  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/detail`
  );
  if (!response.ok) return null;
  const result = await response.json();
  return result.data ?? null;
}

export async function updateFileSelection(
  infoHash: string,
  fileIds: number[]
): Promise<boolean> {
  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/files/select`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ fileIds }),
    }
  );
  if (!response.ok) return false;
  const result = await response.json();
  return result.data?.success ?? false;
}

export interface TorrentPreviewFileApiModel {
  id: number;
  filename: string;
  sizeBytes: number;
  defaultSelected: boolean;
}

export interface TorrentPreviewApiModel {
  name: string;
  infoHash: string;
  totalSizeBytes: number;
  files: TorrentPreviewFileApiModel[];
}

export async function previewTorrent(file: File): Promise<TorrentPreviewApiModel> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/preview`, {
    method: "POST",
    body: formData,
  });

  if (!response.ok) {
    throw new Error("Failed to preview torrent file");
  }

  const result: ApiResponse<TorrentPreviewApiModel> = await response.json();

  if (result.error || !result.data) {
    throw new Error("Preview endpoint returned an error");
  }

  return result.data;
}

export async function addTorrent(file: File, selectedFileIds: number[]): Promise<void> {
  const formData = new FormData();
  formData.append("file", file);
  selectedFileIds.forEach((id) => {
    formData.append("SelectedFileIds", `${id}`);
  });

  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/add`, {
    method: "POST",
    body: formData,
  });

  if (!response.ok) {
    throw new Error("Failed to add torrent");
  }
}
