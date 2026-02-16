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

function decodeBase64(value: string): Uint8Array {
  const raw = atob(value);
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) {
    bytes[i] = raw.charCodeAt(i);
  }
  return bytes;
}

function encodeBase64(bytes: Uint8Array): string {
  const chunkSize = 0x8000;
  const parts: string[] = [];
  for (let i = 0; i < bytes.length; i += chunkSize) {
    const chunk = bytes.subarray(i, i + chunkSize);
    parts.push(String.fromCharCode(...chunk));
  }
  return btoa(parts.join(""));
}

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
    applyVerifiedPiecesToDetailBitfield(
      state,
      action: PayloadAction<{ verifiedPieces: number[] }>
    ) {
      if (
        !state.detail ||
        !state.detail.bitfield.bitfield ||
        action.payload.verifiedPieces.length === 0
      ) {
        return;
      }

      const pieceCount = state.detail.bitfield.pieceCount;
      if (pieceCount <= 0) {
        return;
      }

      const bitfieldBytes = decodeBase64(state.detail.bitfield.bitfield);
      const uniqueIndices = new Set(action.payload.verifiedPieces);
      let newlyVerified = 0;

      for (const pieceIndex of uniqueIndices) {
        if (pieceIndex < 0 || pieceIndex >= pieceCount) {
          continue;
        }

        const byteIndex = pieceIndex >> 3;
        const mask = 1 << (7 - (pieceIndex & 7));
        const hadPiece = (bitfieldBytes[byteIndex] & mask) !== 0;
        if (!hadPiece) {
          bitfieldBytes[byteIndex] |= mask;
          newlyVerified++;
        }
      }

      if (newlyVerified === 0) {
        return;
      }

      state.detail.bitfield.haveCount = Math.min(
        pieceCount,
        state.detail.bitfield.haveCount + newlyVerified
      );
      state.detail.bitfield.bitfield = encodeBase64(bitfieldBytes);
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
  applyVerifiedPiecesToDetailBitfield,
  updateDetailFiles,
  clearDetail,
} = torrentDetailSlice.actions;
export default torrentDetailSlice.reducer;
