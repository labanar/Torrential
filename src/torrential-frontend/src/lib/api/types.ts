export interface TorrentFileInfo {
  fileIndex: number;
  fileName: string;
  fileSize: number;
}

export interface TorrentState {
  infoHash: string;
  name: string;
  totalSize: number;
  files: TorrentFileInfo[];
  selectedFileIndices: number[];
  status: 'Added' | 'Downloading' | 'Stopped' | 'Completed' | 'Error';
  dateAdded: string;
}

export interface AddTorrentFileRequest {
  fileIndex: number;
  fileName: string;
  fileSize: number;
}

export interface AddTorrentFileSelectionRequest {
  fileIndex: number;
  selected: boolean;
}

export interface AddTorrentRequest {
  name: string;
  infoHash: string;
  totalSize: number;
  pieceSize: number;
  numberOfPieces: number;
  files: AddTorrentFileRequest[];
  announceUrls: string[];
  pieceHashes: string;
  fileSelections?: AddTorrentFileSelectionRequest[];
}
