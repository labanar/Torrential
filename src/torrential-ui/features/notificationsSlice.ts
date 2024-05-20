import { IconDefinition } from "@fortawesome/free-solid-svg-icons";
import { createSlice, PayloadAction } from "@reduxjs/toolkit";

export interface ToastNotification {
    title: string;
    description: string;
    status: "info" | "warning" | "success" | "error";
    duration: number;
    isClosable: boolean,
    icon: string | undefined | IconDefinition;
};

export interface NotificationsState {
    toastQueue: ToastNotification[]
    currentToast: ToastNotification | undefined
}



const initialState: NotificationsState = {
    toastQueue: [],
    currentToast: undefined
};

const notificationsSlice = createSlice({
  name: "notifications",
  initialState,
  reducers: {
    queueNotification(state, action: PayloadAction<ToastNotification>) {
        state.toastQueue = [...state.toastQueue, action.payload];
        state.currentToast = action.payload;
    },
    dequeueNext(state) {
        state.currentToast = state.toastQueue.shift();
    }
  },
});

export const { queueNotification, dequeueNext} = notificationsSlice.actions;
export default notificationsSlice.reducer;
