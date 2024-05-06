import { createSelector } from '@reduxjs/toolkit';
import { RootState } from './store';

export const torrentsWithPeersSelector = createSelector(
  (state: RootState) => state.torrents,
  (state: RootState) => state.peers,
  (torrents, peers) => Object.keys(torrents).map(infoHash => ({
    ...torrents[infoHash],
    peers: peers[infoHash] || []
  }))
);