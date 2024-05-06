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
      const { infoHash, peerId, ip, port , isSeed} = action.payload;
      const peer = { infoHash, peerId, ip, port , isSeed};
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
    updatePeer(state, action: PayloadAction<{ infoHash: string; peerId: string; update: Partial<PeerSummary> }>) {
      const { infoHash, peerId, update } = action.payload;
      const peerIndex = state[infoHash].findIndex(p => p.peerId === peerId);
      if (peerIndex !== -1) {
        state[infoHash][peerIndex] = { ...state[infoHash][peerIndex], ...update };
      }
    }
  },
});

export const { addPeer, removePeer, setPeers, updatePeer} = peersSlice.actions;
export default peersSlice.reducer;
