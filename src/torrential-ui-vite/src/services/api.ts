export async function fetchTorrents(): Promise<TorrentApiModel[]> {
  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL}/torrents`
  );

  if (!response.ok) {
    throw new Error("Error fetching torrents");
  }

  const result: ApiResponse<TorrentApiModel[]> = await response.json();
  return result.data ?? [];
}

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

export interface TorrentVerificationStartedEvent {
  infoHash: string;
}

export interface TorrentVerificationCompletedEvent {
  infoHash: string;
  progress?: number;
}

export interface TorrentFileCopyStartedEvent {
  infoHash: string;
  fileName: string;
}

export interface TorrentFileCopyCompletedEvent {
  infoHash: string;
  fileName: string;
  progress?: number;
}

export interface TorrentRemovedEvent {
  infoHash: string;
}

export interface TorrentAddedEvent {
  infoHash: string;
  totalSize: number;
  name: string;
  progress?: number;
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

export interface DirectoryBrowseApiModel {
  currentPath: string;
  parentPath?: string | null;
  canNavigateUp: boolean;
  directories: string[];
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

export async function addTorrent(file: File, selectedFileIds: number[], completedPath?: string): Promise<void> {
  const formData = new FormData();
  formData.append("file", file);
  selectedFileIds.forEach((id) => {
    formData.append("SelectedFileIds", `${id}`);
  });
  if (completedPath) {
    formData.append("CompletedPath", completedPath);
  }

  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/add`, {
    method: "POST",
    body: formData,
  });

  if (!response.ok) {
    throw new Error("Failed to add torrent");
  }
}

export async function browseDirectories(path?: string): Promise<DirectoryBrowseApiModel> {
  const query = path ? `?path=${encodeURIComponent(path)}` : "";
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/filesystem/directories${query}`);

  if (!response.ok) {
    throw new Error("Failed to browse directories");
  }

  const result: ApiResponse<DirectoryBrowseApiModel> = await response.json();
  if (!result.data) {
    throw new Error("Directory browser endpoint returned no data");
  }

  return result.data;
}

// --- Indexer API ---

export interface IndexerVm {
  id: string;
  name: string;
  type: string;
  baseUrl: string;
  authMode: string;
  enabled: boolean;
  dateAdded: string;
}

export interface CreateIndexerRequest {
  name: string;
  type: "Torznab" | "Rss" | "TorrentLeech";
  baseUrl: string;
  authMode: "None" | "ApiKey" | "BasicAuth" | "Cookie";
  apiKey?: string;
  username?: string;
  password?: string;
  enabled: boolean;
}

export interface UpdateIndexerRequest extends CreateIndexerRequest {}

export interface SearchResultVm {
  title: string;
  sizeBytes: number;
  seeders: number;
  leechers: number;
  infoHash: string | null;
  downloadUrl: string | null;
  detailsUrl: string | null;
  category: string | null;
  publishDate: string | null;
  indexerName: string | null;
  metadata: MetadataVm | null;
}

export interface MetadataVm {
  description: string | null;
  externalId: string | null;
  artworkUrl: string | null;
  genre: string | null;
  year: number | null;
}

export async function fetchIndexers(): Promise<IndexerVm[]> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers`);
  if (!response.ok) throw new Error("Failed to fetch indexers");
  const result: ApiResponse<IndexerVm[]> = await response.json();
  return result.data ?? [];
}

export async function fetchIndexer(id: string): Promise<IndexerVm | null> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers/${id}`);
  if (!response.ok) return null;
  const result: ApiResponse<IndexerVm> = await response.json();
  return result.data ?? null;
}

export async function createIndexer(request: CreateIndexerRequest): Promise<IndexerVm> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const err = await response.json().catch(() => null);
    throw new Error(err?.error?.message ?? "Failed to create indexer");
  }
  const result: ApiResponse<IndexerVm> = await response.json();
  return result.data!;
}

export async function updateIndexer(id: string, request: UpdateIndexerRequest): Promise<IndexerVm> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const err = await response.json().catch(() => null);
    throw new Error(err?.error?.message ?? "Failed to update indexer");
  }
  const result: ApiResponse<IndexerVm> = await response.json();
  return result.data!;
}

export async function deleteIndexer(id: string): Promise<boolean> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers/${id}`, {
    method: "DELETE",
  });
  if (!response.ok) return false;
  const result = await response.json();
  return result.data?.success ?? false;
}

export async function testIndexer(id: string): Promise<boolean> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers/${id}/test`, {
    method: "POST",
  });
  if (!response.ok) return false;
  const result = await response.json();
  return result.data?.success ?? false;
}

export interface IndexerSearchRequest {
  query: string;
  category?: string;
  limit?: number;
}

export async function searchIndexers(request: IndexerSearchRequest): Promise<SearchResultVm[]> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/indexers/search`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const err = await response.json().catch(() => null);
    throw new Error(err?.error?.message ?? "Search failed");
  }
  const result: ApiResponse<SearchResultVm[]> = await response.json();
  return result.data ?? [];
}

export async function previewTorrentFromUrl(downloadUrl: string): Promise<TorrentPreviewApiModel> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/preview-url`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ url: downloadUrl }),
  });

  if (!response.ok) {
    throw new Error("Failed to preview torrent from URL");
  }

  const result: ApiResponse<TorrentPreviewApiModel> = await response.json();

  if (result.error || !result.data) {
    throw new Error("Preview URL endpoint returned an error");
  }

  return result.data;
}

export async function addTorrentFromUrl(
  downloadUrl: string,
  selectedFileIds?: number[],
  completedPath?: string,
): Promise<void> {
  const body: Record<string, unknown> = { url: downloadUrl };
  if (selectedFileIds) {
    body.selectedFileIds = selectedFileIds;
  }
  if (completedPath) {
    body.completedPath = completedPath;
  }

  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/add-url`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error("Failed to add torrent from URL");
  }
}
