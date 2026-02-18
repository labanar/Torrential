import { configureStore, createSelector } from "@reduxjs/toolkit";
import torrentsReducer from "./slices/torrentsSlice";
import peersReducer from "./slices/peersSlice";
import notificationsReducer from "./slices/notificationsSlice";
import torrentDetailReducer from "./slices/torrentDetailSlice";
import { useDispatch, useSelector } from "react-redux";
import { ConnectedPeer, TorrentSummary } from "../types";

const store = configureStore({
  reducer: {
    torrents: torrentsReducer,
    peers: peersReducer,
    notifications: notificationsReducer,
    torrentDetail: torrentDetailReducer,
  },
});

export default store;
export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;

export const useAppDispatch = useDispatch.withTypes<AppDispatch>();
export const useAppSelector = useSelector.withTypes<RootState>();

export const torrentsWithPeersSelector = createSelector(
  (state: RootState) => state.torrents,
  (state: RootState) => state.peers,
  (torrents, peers) =>
    Object.keys(torrents).map((infoHash) => ({
      ...torrents[infoHash],
      peers: peers[infoHash] || [],
    }))
);

export const selectAllConnectedPeers = createSelector(
  (state: RootState) => state.torrents,
  (state: RootState) => state.peers,
  (torrents, peers): ConnectedPeer[] =>
    Object.entries(peers).flatMap(([infoHash, peerList]) => {
      const torrent = torrents[infoHash];
      return peerList.map((peer) => ({
        infoHash,
        torrentName: torrent?.name ?? infoHash,
        torrentStatus: torrent?.status ?? "",
        peerId: peer.peerId,
        ip: peer.ip,
        port: peer.port,
        isSeed: peer.isSeed,
      }));
    })
);

export const selectTorrentsByInfoHashes = (infoHashes: string[]) =>
  createSelector(
    (state: RootState) => state.torrents,
    (torrentsState) =>
      infoHashes.reduce<{ [infoHash: string]: TorrentSummary }>(
        (acc, infoHash) => {
          if (torrentsState[infoHash]) {
            acc[infoHash] = torrentsState[infoHash];
          }
          return acc;
        },
        {}
      )
  );
