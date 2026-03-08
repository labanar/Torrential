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
import { Separator } from "@/components/ui/separator";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Card } from "@/components/ui/card";
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
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [theme, setTheme] = useState<Theme>(() => resolveInitialTheme());
  const location = useLocation();

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
    <div className="flex h-full min-h-0 w-full min-w-0 flex-col overflow-hidden bg-background md:flex-row">
      <aside className="hidden h-full w-72 shrink-0 border-r bg-card/30 p-4 md:flex md:flex-col">
        <DesktopNav
          pathname={location.pathname}
          theme={theme}
          onToggleTheme={onToggleTheme}
        />
      </aside>

      <div className="flex min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-14 items-center justify-between border-b bg-background/95 px-3 backdrop-blur md:hidden">
          <div className="flex items-center gap-2">
            <Sheet open={mobileNavOpen} onOpenChange={setMobileNavOpen}>
              <SheetTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Open navigation menu">
                  <FontAwesomeIcon icon={faBars} />
                </Button>
              </SheetTrigger>
              <SheetContent side="left" className="w-[88vw] max-w-xs p-0">
                <SheetHeader className="border-b p-4">
                  <SheetTitle className="text-base tracking-tight">Torrential</SheetTitle>
                </SheetHeader>
                <div className="p-3">
                  <MobileNav
                    pathname={location.pathname}
                    theme={theme}
                    onToggleTheme={onToggleTheme}
                    onNavigate={() => setMobileNavOpen(false)}
                  />
                </div>
              </SheetContent>
            </Sheet>
            <span className="text-sm font-semibold tracking-wide">Torrential</span>
          </div>
          <Button
            variant="outline"
            size="icon"
            onClick={onToggleTheme}
            aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
          >
            <FontAwesomeIcon icon={theme === "dark" ? faSun : faMoon} />
          </Button>
        </header>

        {alfred}
        <main id="main" className="min-h-0 flex-1 overflow-auto">
          {children}
        </main>
      </div>
    </div>
  );
}

interface NavigationProps {
  pathname: string;
  theme: Theme;
  onToggleTheme: () => void;
  onNavigate?: () => void;
}

function DesktopNav({ pathname, theme, onToggleTheme }: NavigationProps) {
  return (
    <Card className="flex min-h-0 flex-1 flex-col border-border/70 bg-card/80 p-3 shadow-sm">
      <div className="px-2 pb-3 pt-1">
        <h1 className="text-base font-semibold tracking-tight">Torrential</h1>
        <p className="text-xs text-muted-foreground">Torrent operations dashboard</p>
      </div>
      <Separator />
      <nav className="mt-3 flex min-h-0 flex-1 flex-col gap-1 overflow-auto">
        <NavItems pathname={pathname} />
      </nav>
      <Separator className="my-3" />
      <ThemeToggleButton theme={theme} onToggleTheme={onToggleTheme} />
    </Card>
  );
}

function MobileNav({ pathname, theme, onToggleTheme, onNavigate }: NavigationProps) {
  return (
    <div className="flex min-h-0 flex-col gap-3">
      <nav className="flex flex-col gap-1">
        <NavItems pathname={pathname} onNavigate={onNavigate} />
      </nav>
      <Separator />
      <ThemeToggleButton theme={theme} onToggleTheme={onToggleTheme} />
    </div>
  );
}

function NavItems({ pathname, onNavigate }: { pathname: string; onNavigate?: () => void }) {
  const navigate = useNavigate();

  return (
    <>
      {navItems.map((item) => {
        const active = pathname === item.href;
        return (
          <button
            key={item.href}
            type="button"
            onClick={() => {
              navigate(item.href);
              onNavigate?.();
            }}
            className={cn(
              "flex min-h-11 w-full items-center gap-3 rounded-md px-3 text-left text-sm font-medium transition-colors",
              active
                ? "bg-primary text-primary-foreground shadow-sm"
                : "text-foreground hover:bg-muted"
            )}
          >
            <FontAwesomeIcon icon={item.icon} className="h-4 w-4" />
            <span>{item.label}</span>
          </button>
        );
      })}
    </>
  );
}

function ThemeToggleButton({ theme, onToggleTheme }: { theme: Theme; onToggleTheme: () => void }) {
  return (
    <Button
      variant="outline"
      className="w-full justify-start gap-3"
      onClick={onToggleTheme}
      aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
    >
      <FontAwesomeIcon icon={theme === "dark" ? faSun : faMoon} className="h-4 w-4" />
      <span>{theme === "dark" ? "Light mode" : "Dark mode"}</span>
    </Button>
  );
}
