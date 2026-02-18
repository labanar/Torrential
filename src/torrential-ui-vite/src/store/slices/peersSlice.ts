import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { PeerSummary } from "../../types";

export interface PeersState {
  [infoHash: string]: PeerSummary[];
}

const initialState: PeersState = {};

const peersSlice = createSlice({
  name: "peers",
  initialState,
  reducers: {
    addPeer(state, action: PayloadAction<PeerSummary>) {
      const { infoHash, peerId, ip, port, isSeed } = action.payload;
      const peer = { infoHash, peerId, ip, port, isSeed };
      const existing = state[infoHash];
      if (!existing) {
        state[infoHash] = [peer];
        return;
      }
      if (existing.some((p) => p.peerId === peerId)) return;
      existing.push(peer);
    },
    removePeer(
      state,
      action: PayloadAction<{ infoHash: string; peerId: string }>
    ) {
      const { infoHash, peerId } = action.payload;
      const peers = state[infoHash];
      if (!peers) return;
      state[infoHash] = peers.filter((p) => p.peerId !== peerId);
    },
    setPeers(
      state,
      action: PayloadAction<{ infoHash: string; peers: PeerSummary[] }>
    ) {
      const { infoHash, peers } = action.payload;
      state[infoHash] = peers;
    },
    clearPeers(state, action: PayloadAction<{ infoHash: string }>) {
      delete state[action.payload.infoHash];
    },
    updatePeer(
      state,
      action: PayloadAction<{
        infoHash: string;
        peerId: string;
        update: Partial<PeerSummary>;
      }>
    ) {
      const { infoHash, peerId, update } = action.payload;
      const peers = state[infoHash];
      if (!peers) return;
      const peerIndex = peers.findIndex((p) => p.peerId === peerId);
      if (peerIndex !== -1) {
        peers[peerIndex] = { ...peers[peerIndex], ...update };
      }
    },
  },
});

export const { addPeer, removePeer, setPeers, clearPeers, updatePeer } = peersSlice.actions;
export default peersSlice.reducer;
