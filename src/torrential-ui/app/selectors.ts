import { createSelector } from '@reduxjs/toolkit';
import { RootState } from './store';
import { TorrentSummary } from '@/types';

export const torrentsWithPeersSelector = createSelector(
  (state: RootState) => state.torrents,
  (state: RootState) => state.peers,
  (torrents, peers) => Object.keys(torrents).map(infoHash => ({
    ...torrents[infoHash],
    peers: peers[infoHash] || []
  }))
);


export const selectTorrentsByInfoHashes = (infoHashes: string[]) =>
  createSelector(
    (state: RootState) => state.torrents,
    (torrentsState) =>
      infoHashes.reduce<{ [infoHash: string]: TorrentSummary }>((acc, infoHash) => {
        if (torrentsState[infoHash]) {
          acc[infoHash] = torrentsState[infoHash];
        }
        return acc;
      }, {})
  );