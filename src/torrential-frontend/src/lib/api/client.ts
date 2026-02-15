import type { TorrentState, AddTorrentRequest } from './types';

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

export async function addTorrent(request: AddTorrentRequest): Promise<void> {
  const res = await fetch(`${BASE}/torrents`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
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

export async function removeTorrent(infoHash: string, deleteData = false): Promise<void> {
  const res = await fetch(`${BASE}/torrents/${infoHash}?deleteData=${deleteData}`, { method: 'DELETE' });
  if (!res.ok) throw new Error('Failed to remove torrent');
}
