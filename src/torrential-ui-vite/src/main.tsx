import * as React from "react";
import * as ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import "./index.css";
import { ChakraProvider, ColorModeScript, extendTheme } from "@chakra-ui/react";
import TorrentPage from "./pages/torrent";
import { Provider } from "react-redux";
import store from "./store";
import { HotkeysProvider } from "react-hotkeys-hook";
import ToastNotification from "./components/ToastNotification/toast-notification";
import PeersPage from "./pages/peers";
import IntegrationsPage from "./pages/integrations";
import SettingsPage from "./pages/settings";

const router = createBrowserRouter([
  {
    path: "/",
    element: <TorrentPage />,
  },
  {
    path: "/peers",
    element: <PeersPage />,
  },
  {
    path: "integrations",
    element: <IntegrationsPage />,
  },
  {
    path: "settings",
    element: <SettingsPage />,
  },
]);

const theme = extendTheme({
  initialColorMode: "system",
  useSystemColorMode: true,
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ColorModeScript initialColorMode={theme.config.initialColorMode} />
    <Provider store={store}>
      <ChakraProvider theme={theme}>
        <HotkeysProvider initiallyActiveScopes={["global"]}>
          <ToastNotification />
          <RouterProvider router={router} />
        </HotkeysProvider>
      </ChakraProvider>
    </Provider>
  </React.StrictMode>
);
