import type { TorrentState } from './types';

const BASE = '/api';

export async function listTorrents(): Promise<TorrentState[]> {
  const res = await fetch(`${BASE}/torrents`);
  if (!res.ok) throw new Error('Failed to fetch torrents');
  const body = await res.json();
  return body.data ?? [];
}

export async function getTorrent(infoHash: string): Promise<TorrentState | null> {
  const res = await fetch(`${BASE}/torrents/${infoHash}`);
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Failed to fetch torrent');
  const body = await res.json();
  return body.data ?? null;
}

export async function addTorrent(file: File): Promise<void> {
  const formData = new FormData();
  formData.append('file', file);
  const res = await fetch(`${BASE}/torrents/add`, {
    method: 'POST',
    body: formData,
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
