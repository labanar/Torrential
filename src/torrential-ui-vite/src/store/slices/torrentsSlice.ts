export interface TorrentsState {
  [infoHash: string]: TorrentSummary;
}

import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { TorrentSummary } from "../../types";

const initialState: TorrentsState = {};

const torrentsSlice = createSlice({
  name: "torrents",
  initialState,
  reducers: {
    setTorrents(
      state,
      action: PayloadAction<{ [key: string]: TorrentSummary }>
    ) {
      return { ...state, ...action.payload };
    },
    updateTorrent(
      state,
      action: PayloadAction<{
        infoHash: string;
        update: Partial<TorrentSummary>;
      }>
    ) {
      const { infoHash, update } = action.payload;
      state[infoHash] = { ...state[infoHash], ...update };
    },
    removeTorrent(state, action: PayloadAction<{ infoHash: string }>) {
      const { infoHash } = action.payload;
      delete state[infoHash];
    },
  },
});

export const { setTorrents, updateTorrent, removeTorrent } =
  torrentsSlice.actions;
export default torrentsSlice.reducer;
