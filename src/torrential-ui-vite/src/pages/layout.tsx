import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  faBars,
  faGear,
  faMoon,
  faPlug,
  faSun,
  faUpDown,
  faUsers,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useLocation, useNavigate } from "react-router-dom";
import Alfred from "../components/Alfred/alfred";
import SignalRService from "../services/signalR";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import { Theme, applyTheme, resolveInitialTheme } from "@/lib/theme";
import { cn } from "@/lib/utils";

config.autoAddCss = false;

interface NavItem {
  label: string;
  href: string;
  icon: typeof faUpDown;
}

const navItems: NavItem[] = [
  { label: "Torrents", href: "/", icon: faUpDown },
  { label: "Peers", href: "/peers", icon: faUsers },
  { label: "Integrations", href: "/integrations", icon: faPlug },
  { label: "Settings", href: "/settings", icon: faGear },
];

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const [theme, setTheme] = useState<Theme>(() => resolveInitialTheme());
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

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

  return (
    <div className="flex h-full min-h-0 w-full min-w-0 flex-col overflow-hidden bg-background">
      <header className="flex h-12 shrink-0 items-center justify-between border-b px-4">
        <div className="flex items-center gap-6">
          {/* Mobile hamburger */}
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 sm:hidden"
            onClick={() => setMobileNavOpen(true)}
            aria-label="Open navigation menu"
          >
            <FontAwesomeIcon icon={faBars} className="h-4 w-4" />
          </Button>

          <span className="text-sm font-semibold tracking-tight">Torrential</span>

          {/* Desktop nav */}
          <nav className="hidden items-center gap-1 sm:flex">
            {navItems.map((item) => {
              const active = location.pathname === item.href;
              return (
                <button
                  key={item.href}
                  type="button"
                  onClick={() => navigate(item.href)}
                  className={cn(
                    "flex items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
                    active
                      ? "bg-muted text-foreground"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  <FontAwesomeIcon icon={item.icon} className="h-3.5 w-3.5" />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </div>

        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          onClick={onToggleTheme}
          aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
        >
          <FontAwesomeIcon icon={theme === "dark" ? faSun : faMoon} className="h-4 w-4" />
        </Button>
      </header>

      {/* Mobile slide-out nav */}
      <Sheet open={mobileNavOpen} onOpenChange={setMobileNavOpen}>
        <SheetContent side="left" className="w-64 p-0">
          <SheetTitle className="sr-only">Navigation</SheetTitle>
          <nav className="flex flex-col gap-1 p-4 pt-12">
            {navItems.map((item) => {
              const active = location.pathname === item.href;
              return (
                <button
                  key={item.href}
                  type="button"
                  onClick={() => {
                    navigate(item.href);
                    setMobileNavOpen(false);
                  }}
                  className={cn(
                    "flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-medium transition-colors",
                    active
                      ? "bg-muted text-foreground"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  <FontAwesomeIcon icon={item.icon} className="h-4 w-4" />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </SheetContent>
      </Sheet>

      {alfred}
      <main id="main" className="min-h-0 flex-1 overflow-auto">
        {children}
      </main>
    </div>
  );
}
