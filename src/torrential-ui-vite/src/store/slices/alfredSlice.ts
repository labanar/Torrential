import { createSlice, PayloadAction } from "@reduxjs/toolkit";

// export interface ToastNotification {
//     title: string;
//     description: string;
//     status: "info" | "warning" | "success" | "error";
//     duration: number;
//     isClosable: boolean,
//     icon: string | undefined | IconDefinition;
// };

export enum AlfredContext {
  Global,
  TorrentList,
}

export interface AlfredState {
  context: AlfredContext;
}

const initialState: AlfredState = {
  context: AlfredContext.Global,
};

const alfredSlice = createSlice({
  name: "notifications",
  initialState,
  reducers: {
    setContext(state, action: PayloadAction<AlfredContext>) {
      state.context = action.payload;
    },
    // queueNotification(state, action: PayloadAction<ToastNotification>) {
    //     state.toastQueue = [...state.toastQueue, action.payload];
    //     state.currentToast = action.payload;
    // },
    // dequeueNext(state) {
    //     state.currentToast = state.toastQueue.shift();
    // }
  },
});

export const { setContext } = alfredSlice.actions;
export default alfredSlice.reducer;
