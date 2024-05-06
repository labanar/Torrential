"use client";

import { ChakraProvider } from "@chakra-ui/react";
import { Provider } from "react-redux";
import store from "./store";

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <Provider store={store}>
      <ChakraProvider>{children}</ChakraProvider>
    </Provider>
  );
}
