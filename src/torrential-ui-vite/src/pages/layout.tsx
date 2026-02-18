import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useCallback, useEffect, useMemo, useState } from "react";
import styles from "./layout.module.css";
import {
  IconDefinition,
  faBars,
  faGear,
  faMoon,
  faPlug,
  faSun,
  faUpDown,
  faUsers,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useLocation, useNavigate } from "react-router-dom";
import Alfred from "../components/Alfred/alfred";
import SignalRService from "../services/signalR";
import { Separator } from "@/components/ui/separator";
config.autoAddCss = false;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

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
    if (!isMobileSidebarOpen) {
      return;
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsMobileSidebarOpen(false);
      }
    };

    window.addEventListener("keydown", handleEscape);
    return () => window.removeEventListener("keydown", handleEscape);
  }, [isMobileSidebarOpen]);

  const alfred = useMemo(() => <Alfred />, []);

  return (
    <div className={styles.root}>
      <div className={styles.mobileTopBar}>
        <button
          type="button"
          className={styles.mobileMenuButton}
          onClick={() => setIsMobileSidebarOpen(true)}
          aria-label="Open navigation menu"
        >
          <FontAwesomeIcon icon={faBars} />
        </button>
        <span className={styles.mobileTitle}>Torrential</span>
      </div>
      <button
        type="button"
        aria-label="Close navigation menu"
        className={`${styles.mobileBackdrop} ${
          isMobileSidebarOpen ? styles.mobileBackdropVisible : ""
        }`}
        onClick={() => setIsMobileSidebarOpen(false)}
      />
      <SideBar
        isMobileSidebarOpen={isMobileSidebarOpen}
        closeMobileSidebar={() => setIsMobileSidebarOpen(false)}
      />
      {alfred}
      <div className={styles.divider}>
        <Separator orientation="vertical" className={styles.verticalDivider} />
      </div>
      <div id="main" className={styles.main}>
        {children}
      </div>
    </div>
  );
}

interface SideBarProps {
  isMobileSidebarOpen: boolean;
  closeMobileSidebar: () => void;
}

function SideBar({ isMobileSidebarOpen, closeMobileSidebar }: SideBarProps) {
  const location = useLocation();
  const [theme, setTheme] = useState<"light" | "dark">(() =>
    document.documentElement.classList.contains("dark") ? "dark" : "light"
  );

  const toggleTheme = useCallback(() => {
    const nextTheme = theme === "dark" ? "light" : "dark";
    setTheme(nextTheme);
    document.documentElement.classList.toggle("dark", nextTheme === "dark");
    document.documentElement.style.colorScheme = nextTheme;
    window.localStorage.setItem("theme", nextTheme);
  }, [theme]);

  return (
    <div
      id="sidebar"
      className={`${styles.sidebar} ${isMobileSidebarOpen ? styles.sidebarOpen : ""}`}
    >
      <div className={styles.sidebarHeader}>
        <span className={styles.sidebarTitle}>Torrential</span>
        <button
          type="button"
          className={styles.mobileCloseButton}
          onClick={closeMobileSidebar}
          aria-label="Close navigation menu"
        >
          <FontAwesomeIcon icon={faXmark} />
        </button>
      </div>
      <SideBarItem
        label="Torrents"
        linksTo="/"
        icon={faUpDown}
        isActive={location.pathname === "/"}
        onNavigate={closeMobileSidebar}
      />
      <SideBarItem
        label="Peers"
        linksTo="/peers"
        icon={faUsers}
        isActive={location.pathname === "/peers"}
        onNavigate={closeMobileSidebar}
      />
      <SideBarItem
        label="Integrations"
        linksTo="/integrations"
        icon={faPlug}
        isActive={location.pathname === "/integrations"}
        onNavigate={closeMobileSidebar}
      />
      <SideBarItem
        label="Settings"
        linksTo="/settings"
        icon={faGear}
        isActive={location.pathname === "/settings"}
        onNavigate={closeMobileSidebar}
      />

      <div className={styles.sidebarFooter}>
        <button
          type="button"
          onClick={toggleTheme}
          className={styles.themeToggle}
          aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
        >
          <FontAwesomeIcon icon={theme === "dark" ? faSun : faMoon} fontSize={24} />
        </button>
      </div>
    </div>
  );
}

interface SideBarItemProps {
  label: string;
  linksTo: string;
  icon: IconDefinition;
  isActive: boolean;
  onNavigate: () => void;
}

function SideBarItem({
  label,
  linksTo,
  icon,
  isActive,
  onNavigate,
}: SideBarItemProps) {
  const navigate = useNavigate();

  return (
    <button
      type="button"
      className={`${styles.sidebarItem} ${isActive ? styles.sidebarItemActive : ""}`}
      onClick={() => {
        navigate(linksTo);
        onNavigate();
      }}
    >
      <span className={styles.sidebarIcon}>
        <FontAwesomeIcon icon={icon} size={"lg"} />
      </span>
      <span className={styles.sidebarItemText}>{label}</span>
    </button>
  );
}
