import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import {
  faBars,
  faBell,
  faCircleDown,
  faCircleCheck,
  faCirclePause,
  faCirclePlay,
  faGear,
  faList,
  faMoon,
  faPlus,
  faQuestionCircle,
  faRightFromBracket,
  faSun,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useLocation, useNavigate } from "react-router-dom";
import Alfred from "../components/Alfred/alfred";
import SignalRService from "../services/signalR";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { Theme, applyTheme, resolveInitialTheme } from "@/lib/theme";
import { cn } from "@/lib/utils";

config.autoAddCss = false;

export type StatusFilter = "all" | "downloading" | "completed" | "paused" | "active";

interface LayoutContextValue {
  filterText: string;
  setFilterText: (text: string) => void;
  statusFilter: StatusFilter;
  setStatusFilter: (filter: StatusFilter) => void;
  openedInfoHash: string | null;
  setOpenedInfoHash: (hash: string | null) => void;
}

const LayoutContext = createContext<LayoutContextValue>({
  filterText: "",
  setFilterText: () => {},
  statusFilter: "all",
  setStatusFilter: () => {},
  openedInfoHash: null,
  setOpenedInfoHash: () => {},
});

export function useLayoutContext() {
  return useContext(LayoutContext);
}

interface SidebarFilterItem {
  label: string;
  value: StatusFilter;
  icon: typeof faList;
}

const sidebarFilters: SidebarFilterItem[] = [
  { label: "All Torrents", value: "all", icon: faList },
  { label: "Downloading", value: "downloading", icon: faCircleDown },
  { label: "Completed", value: "completed", icon: faCircleCheck },
  { label: "Paused", value: "paused", icon: faCirclePause },
  { label: "Active", value: "active", icon: faCirclePlay },
];

export const statusFilterLabels: Record<StatusFilter, string> = {
  all: "All Torrents",
  downloading: "Downloading",
  completed: "Completed",
  paused: "Paused",
  active: "Active",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const [theme, setTheme] = useState<Theme>(() => resolveInitialTheme());
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [filterText, setFilterText] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [openedInfoHash, setOpenedInfoHash] = useState<string | null>(null);
  const location = useLocation();
  const navigate = useNavigate();

  const isHomePage = location.pathname === "/";

  useEffect(() => {
    const signalRService = new SignalRService(
      `${import.meta.env.VITE_API_BASE_URL}/torrents/hub`
    );
    signalRService.startConnection();

    return () => {
      signalRService.stopConnection();
    };
  }, []);

  useEffect(() => {
    setMobileNavOpen(false);
  }, [location.pathname]);

  const onToggleTheme = useCallback(() => {
    const nextTheme = theme === "dark" ? "light" : "dark";
    setTheme(nextTheme);
    applyTheme(nextTheme);
  }, [theme]);

  const alfred = useMemo(() => <Alfred />, []);

  const contextValue = useMemo<LayoutContextValue>(
    () => ({ filterText, setFilterText, statusFilter, setStatusFilter, openedInfoHash, setOpenedInfoHash }),
    [filterText, statusFilter, openedInfoHash]
  );

  const sidebarContent = (
    <div className="flex h-full flex-col">
      {/* Branding */}
      <div className="px-5 pt-5 pb-4">
        <h1 className="text-base font-bold tracking-tight text-foreground">Obsidian Torrent</h1>
        <p className="text-xs text-muted-foreground">P2P File Sharing v1.0</p>
      </div>

      {/* Search */}
      <div className="px-4 pb-3">
        <Input
          placeholder="Search torrents..."
          value={filterText}
          onChange={(e) => setFilterText(e.target.value)}
          className="h-8 bg-background/50 text-xs"
        />
      </div>

      {/* Filter nav */}
      <nav className="flex flex-1 flex-col gap-0.5 px-3">
        {sidebarFilters.map((item) => {
          const active = statusFilter === item.value && isHomePage;
          return (
            <button
              key={item.value}
              type="button"
              onClick={() => {
                setStatusFilter(item.value);
                if (!isHomePage) navigate("/");
                setMobileNavOpen(false);
              }}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                active
                  ? "border-l-2 border-primary bg-primary/10 text-primary"
                  : "border-l-2 border-transparent text-muted-foreground hover:bg-muted/50 hover:text-foreground"
              )}
            >
              <FontAwesomeIcon icon={item.icon} className="h-3.5 w-3.5" />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      {/* Bottom links */}
      <div className="border-t px-3 py-3 space-y-0.5">
        <button
          type="button"
          className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <FontAwesomeIcon icon={faQuestionCircle} className="h-3.5 w-3.5" />
          <span>Help</span>
        </button>
        <button
          type="button"
          className="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground"
        >
          <FontAwesomeIcon icon={faRightFromBracket} className="h-3.5 w-3.5" />
          <span>Logout</span>
        </button>
      </div>
    </div>
  );

  return (
    <LayoutContext.Provider value={contextValue}>
      <div className="flex h-full min-h-0 w-full min-w-0 overflow-hidden bg-background">
        {/* Desktop sidebar */}
        <aside className="hidden w-56 shrink-0 flex-col border-r bg-sidebar md:flex">
          {sidebarContent}
        </aside>

        {/* Mobile slide-out nav */}
        <Sheet open={mobileNavOpen} onOpenChange={setMobileNavOpen}>
          <SheetContent side="left" className="w-64 bg-sidebar p-0">
            <SheetTitle className="sr-only">Navigation</SheetTitle>
            {sidebarContent}
          </SheetContent>
        </Sheet>

        {/* Right section: header + content */}
        <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
          {/* Top header bar */}
          <header className="flex h-12 shrink-0 items-center justify-between border-b px-4">
            <div className="flex items-center gap-3">
              {/* Mobile hamburger */}
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 md:hidden"
                onClick={() => setMobileNavOpen(true)}
                aria-label="Open navigation menu"
              >
                <FontAwesomeIcon icon={faBars} className="h-4 w-4" />
              </Button>

              {/* Header search (visible on md+) */}
              <div className="hidden md:block">
                <Input
                  placeholder="Search torrents..."
                  value={filterText}
                  onChange={(e) => setFilterText(e.target.value)}
                  className="h-8 w-64 bg-muted/50 text-xs"
                />
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => {
                  navigate("/");
                  // Trigger add torrent via a custom event that the torrent page listens to
                  window.dispatchEvent(new CustomEvent("trigger-add-torrent"));
                }}
                className="hidden text-sm text-muted-foreground transition-colors hover:text-primary sm:inline-flex items-center gap-1.5"
              >
                <FontAwesomeIcon icon={faPlus} className="h-3 w-3" />
                Add Torrent
              </button>

              <button
                type="button"
                onClick={() => navigate("/settings")}
                className="hidden text-sm text-muted-foreground transition-colors hover:text-primary sm:inline-flex items-center gap-1.5"
              >
                <FontAwesomeIcon icon={faGear} className="h-3 w-3" />
                Settings
              </button>

              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-muted-foreground"
                aria-label="Notifications"
              >
                <FontAwesomeIcon icon={faBell} className="h-4 w-4" />
              </Button>

              <div className="h-7 w-7 rounded-full bg-primary/20 border border-primary/30" aria-label="User avatar" />

              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8"
                onClick={onToggleTheme}
                aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
              >
                <FontAwesomeIcon icon={theme === "dark" ? faSun : faMoon} className="h-4 w-4" />
              </Button>
            </div>
          </header>

          {alfred}
          <main id="main" className="min-h-0 flex-1 overflow-hidden">
            {children}
          </main>
        </div>
      </div>
    </LayoutContext.Provider>
  );
}
