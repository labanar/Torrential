"use client";

import { ChakraProvider, extendTheme } from "@chakra-ui/react";
import { Provider } from "react-redux";
import store from "./store";

const theme = extendTheme({
  config: {
    initialColorMode: 'dark'
  },
  
})

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <Provider store={store}>
      <ChakraProvider theme={theme}>
          {children}
      </ChakraProvider>
    </Provider>
  );
}
