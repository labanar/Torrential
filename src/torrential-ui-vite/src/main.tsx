import * as React from "react";
import * as ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import "./index.css";
import TorrentPage from "./pages/torrent";
import { Provider } from "react-redux";
import store from "./store";
import { HotkeysProvider } from "react-hotkeys-hook";
import { Toaster } from "@/components/ui/toaster";
import PeersPage from "./pages/peers";
import IntegrationsPage from "./pages/integrations";
import SettingsPage from "./pages/settings";
import { applyInitialTheme } from "./lib/theme";

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

applyInitialTheme();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Provider store={store}>
      <HotkeysProvider initiallyActiveScopes={["global"]}>
        <Toaster />
        <RouterProvider router={router} />
      </HotkeysProvider>
    </Provider>
  </React.StrictMode>
);
