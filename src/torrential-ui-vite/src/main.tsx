import * as React from "react";
import * as ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import "./index.css";
import TorrentPage from "./pages/torrent";
import { Provider } from "react-redux";
import store from "./store";
import { HotkeysProvider } from "react-hotkeys-hook";
import ToastNotification from "./components/ToastNotification/toast-notification";
import { Toaster } from "@/components/ui/toaster";
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

const applyInitialTheme = () => {
  const savedTheme = window.localStorage.getItem("theme");
  const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  const shouldUseDarkMode = savedTheme ? savedTheme === "dark" : prefersDark;

  document.documentElement.classList.toggle("dark", shouldUseDarkMode);
  document.documentElement.style.colorScheme = shouldUseDarkMode ? "dark" : "light";
};

applyInitialTheme();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Provider store={store}>
      <HotkeysProvider initiallyActiveScopes={["global"]}>
        <ToastNotification />
        <Toaster />
        <RouterProvider router={router} />
      </HotkeysProvider>
    </Provider>
  </React.StrictMode>
);
