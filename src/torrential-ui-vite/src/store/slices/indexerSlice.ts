import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { IndexerVm, SearchResultVm } from "../../services/api";

export interface IndexerState {
  indexers: IndexerVm[];
  indexersLoading: boolean;
  indexersError: string | null;
  searchResults: SearchResultVm[];
  searchLoading: boolean;
  searchError: string | null;
  searchQuery: string;
}

const initialState: IndexerState = {
  indexers: [],
  indexersLoading: false,
  indexersError: null,
  searchResults: [],
  searchLoading: false,
  searchError: null,
  searchQuery: "",
};

const indexerSlice = createSlice({
  name: "indexer",
  initialState,
  reducers: {
    setIndexersLoading(state) {
      state.indexersLoading = true;
      state.indexersError = null;
    },
    setIndexers(state, action: PayloadAction<IndexerVm[]>) {
      state.indexers = action.payload;
      state.indexersLoading = false;
      state.indexersError = null;
    },
    setIndexersError(state, action: PayloadAction<string>) {
      state.indexersLoading = false;
      state.indexersError = action.payload;
    },
    addIndexer(state, action: PayloadAction<IndexerVm>) {
      state.indexers.push(action.payload);
    },
    updateIndexerInList(state, action: PayloadAction<IndexerVm>) {
      const idx = state.indexers.findIndex((i) => i.id === action.payload.id);
      if (idx !== -1) state.indexers[idx] = action.payload;
    },
    removeIndexer(state, action: PayloadAction<string>) {
      state.indexers = state.indexers.filter((i) => i.id !== action.payload);
    },
    setSearchLoading(state) {
      state.searchLoading = true;
      state.searchError = null;
    },
    setSearchResults(state, action: PayloadAction<{ query: string; results: SearchResultVm[] }>) {
      state.searchResults = action.payload.results;
      state.searchQuery = action.payload.query;
      state.searchLoading = false;
      state.searchError = null;
    },
    setSearchError(state, action: PayloadAction<string>) {
      state.searchLoading = false;
      state.searchError = action.payload;
    },
    clearSearchResults(state) {
      state.searchResults = [];
      state.searchQuery = "";
      state.searchError = null;
    },
  },
});

export const {
  setIndexersLoading,
  setIndexers,
  setIndexersError,
  addIndexer,
  updateIndexerInList,
  removeIndexer,
  setSearchLoading,
  setSearchResults,
  setSearchError,
  clearSearchResults,
} = indexerSlice.actions;

export default indexerSlice.reducer;
