import { configureStore } from "@reduxjs/toolkit";
import torrentsReducer from "../features/torrentsSlice";
import peersReducer from "../features/peersSlice";
import notificationsReducer from "../features/notificationsSlice";

const store = configureStore({
  reducer: {
    torrents: torrentsReducer,
    peers: peersReducer,
    notifications: notificationsReducer
  },
});

export default store;
export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
