import { configureStore } from "@reduxjs/toolkit";
import torrentsReducer from "../features/torrentsSlice";
import peersReducer from "../features/peersSlice";

const store = configureStore({
  reducer: {
    torrents: torrentsReducer,
    peers: peersReducer,
  },
});

export default store;
export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
