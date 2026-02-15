import type { TorrentState, ParsedTorrent, TorrentDetails, Settings } from './types';

const BASE = '/api';

export async function listTorrents(): Promise<TorrentState[]> {
  const res = await fetch(`${BASE}/torrents`);
  if (!res.ok) throw new Error('Failed to fetch torrents');
  return res.json();
}

export async function getTorrent(infoHash: string): Promise<TorrentState | null> {
  const res = await fetch(`${BASE}/torrents/${infoHash}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Failed to fetch torrent');
  return res.json();
}

export async function parseTorrent(file: File): Promise<ParsedTorrent> {
  const formData = new FormData();
  formData.append('file', file);
  const res = await fetch(`${BASE}/torrents/parse`, {
    method: 'POST',
    body: formData,
  });
  if (!res.ok) throw new Error('Failed to parse torrent');
  return res.json();
}

export async function addTorrentWithSelections(
  metadata: ParsedTorrent,
  fileSelections: { fileIndex: number; selected: boolean }[]
): Promise<void> {
  const res = await fetch(`${BASE}/torrents`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: metadata.name,
      infoHash: metadata.infoHash,
      totalSize: metadata.totalSize,
      pieceSize: metadata.pieceSize,
      numberOfPieces: metadata.numberOfPieces,
      files: metadata.files.map(f => ({
        fileIndex: f.fileIndex,
        fileName: f.fileName,
        fileSize: f.fileSize,
      })),
      announceUrls: metadata.announceUrls,
      pieceHashes: metadata.pieceHashes,
      fileSelections,
    }),
  });
  if (!res.ok) throw new Error('Failed to add torrent');
}

export async function startTorrent(infoHash: string): Promise<void> {
  const res = await fetch(`${BASE}/torrents/${infoHash}/start`, { method: 'POST' });
  if (!res.ok) throw new Error('Failed to start torrent');
}

export async function stopTorrent(infoHash: string): Promise<void> {
  const res = await fetch(`${BASE}/torrents/${infoHash}/stop`, { method: 'POST' });
  if (!res.ok) throw new Error('Failed to stop torrent');
}

export async function removeTorrent(infoHash: string, deleteFiles = false): Promise<void> {
  const res = await fetch(`${BASE}/torrents/${infoHash}?deleteData=${deleteFiles}`, {
    method: 'DELETE',
  });
  if (!res.ok) throw new Error('Failed to remove torrent');
}

export async function getTorrentDetails(infoHash: string): Promise<TorrentDetails | null> {
  const res = await fetch(`${BASE}/torrents/${infoHash}/details`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Failed to fetch torrent details');
  return res.json();
}

export async function updateFileSelections(
  infoHash: string,
  fileSelections: { fileIndex: number; selected: boolean }[]
): Promise<void> {
  const res = await fetch(`${BASE}/torrents/${infoHash}/files`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ fileSelections }),
  });
  if (!res.ok) throw new Error('Failed to update file selections');
}

export async function getSettings(): Promise<Settings> {
  const res = await fetch(`${BASE}/settings`);
  if (!res.ok) throw new Error('Failed to fetch settings');
  return res.json();
}

export async function updateSettings(settings: Omit<Settings, 'id'>): Promise<Settings> {
  const res = await fetch(`${BASE}/settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  });
  if (!res.ok) throw new Error('Failed to update settings');
  return res.json();
}
