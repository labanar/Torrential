import { PeerSummary } from "@/types";
import { createSlice, PayloadAction } from "@reduxjs/toolkit";

export interface PeersState {
  [infoHash: string]: PeerSummary[];
}

const initialState: PeersState = {};

const peersSlice = createSlice({
  name: "peers",
  initialState,
  reducers: {
    addPeer(state, action: PayloadAction<PeerSummary>) {
      const { infoHash, peerId, ip, port } = action.payload;
      const peer = { infoHash, peerId, ip, port };
      state[infoHash] = state[infoHash] ? [...state[infoHash], peer] : [peer];
    },
    removePeer(
      state,
      action: PayloadAction<{ infoHash: string; peerId: string }>
    ) {
      const { infoHash, peerId } = action.payload;
      state[infoHash] = state[infoHash].filter((p) => p.peerId !== peerId);
    },
    setPeers(state, action: PayloadAction<{ infoHash: string; peers: PeerSummary[] }>) {
      const { infoHash, peers } = action.payload;
      state[infoHash] = peers;  // Replaces the peers list for a specific infoHash
    },
  },
});

export const { addPeer, removePeer, setPeers } = peersSlice.actions;
export default peersSlice.reducer;
