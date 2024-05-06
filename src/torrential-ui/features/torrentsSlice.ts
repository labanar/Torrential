import { TorrentSummary } from "@/types";

export interface TorrentsState {
  [infoHash: string]: TorrentSummary;
}

import { createSlice, PayloadAction } from "@reduxjs/toolkit";

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
  },
});

export const { setTorrents, updateTorrent } = torrentsSlice.actions;
export default torrentsSlice.reducer;
