import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { TorrentDetail } from "../../types";

export interface TorrentDetailState {
  detail: TorrentDetail | null;
  loading: boolean;
  error: string | null;
  selectedInfoHash: string | null;
}

const initialState: TorrentDetailState = {
  detail: null,
  loading: false,
  error: null,
  selectedInfoHash: null,
};

const torrentDetailSlice = createSlice({
  name: "torrentDetail",
  initialState,
  reducers: {
    selectTorrentForDetail(state, action: PayloadAction<string | null>) {
      state.selectedInfoHash = action.payload;
      if (action.payload === null) {
        state.detail = null;
        state.error = null;
      }
    },
    fetchDetailStart(state) {
      state.loading = true;
      state.error = null;
    },
    fetchDetailSuccess(state, action: PayloadAction<TorrentDetail>) {
      state.detail = action.payload;
      state.loading = false;
      state.error = null;
    },
    fetchDetailError(state, action: PayloadAction<string>) {
      state.loading = false;
      state.error = action.payload;
    },
    updateDetailBitfield(
      state,
      action: PayloadAction<{ haveCount: number; bitfield: string }>
    ) {
      if (state.detail) {
        state.detail.bitfield = {
          ...state.detail.bitfield,
          ...action.payload,
        };
      }
    },
    updateDetailFiles(
      state,
      action: PayloadAction<{ selectedFileIds: number[] }>
    ) {
      if (state.detail) {
        const selectedSet = new Set(action.payload.selectedFileIds);
        state.detail.files = state.detail.files.map((f) => ({
          ...f,
          isSelected: selectedSet.has(f.id),
        }));
      }
    },
    clearDetail(state) {
      state.detail = null;
      state.loading = false;
      state.error = null;
      state.selectedInfoHash = null;
    },
  },
});

export const {
  selectTorrentForDetail,
  fetchDetailStart,
  fetchDetailSuccess,
  fetchDetailError,
  updateDetailBitfield,
  updateDetailFiles,
  clearDetail,
} = torrentDetailSlice.actions;
export default torrentDetailSlice.reducer;
