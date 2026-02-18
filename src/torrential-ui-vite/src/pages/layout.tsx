import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useCallback, useEffect, useMemo, useState } from "react";
import styles from "./layout.module.css";
import {
  IconDefinition,
  faGear,
  faMoon,
  faPlug,
  faSun,
  faUmbrella,
  faUpDown,
  faUsers,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useNavigate } from "react-router-dom";
import Alfred from "../components/Alfred/alfred";
import SignalRService from "../services/signalR";
import { Separator } from "@/components/ui/separator";
config.autoAddCss = false;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  useEffect(() => {
    const signalRService = new SignalRService(
      `${import.meta.env.VITE_API_BASE_URL}/torrents/hub`
    );
    signalRService.startConnection();

    return () => {
      signalRService.stopConnection();
    };
  }, []);

  const alfred = useMemo(() => <Alfred />, []);

  return (
    <div className={styles.root}>
      <SideBar />
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

function SideBar() {
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
    <div id="sidebar" className={styles.sidebar}>
      <FontAwesomeIcon
        icon={faUmbrella}
        size={"6x"}
        style={{
          textAlign: "center",
          alignSelf: "center",
          paddingBottom: "0.1em",
          paddingTop: "0.3em",
          opacity: 0.1,
        }}
      />
      <span className={styles.sidebarTitle}>TORRENTIAL</span>
      <SideBarItem label="TORRENTS" linksTo="/" icon={faUpDown} />
      <SideBarItem label="PEERS" linksTo="/peers" icon={faUsers} />
      <SideBarItem label="INTEGRATIONS" linksTo="/integrations" icon={faPlug} />
      <SideBarItem label="SETTINGS" linksTo="/settings" icon={faGear} />

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
}

function SideBarItem({ label, linksTo, icon }: SideBarItemProps) {
  const navigate = useNavigate();

  return (
    <button
      type="button"
      className={styles.sidebarItem}
      onClick={() => navigate(linksTo)}
    >
      <span className={styles.sidebarIcon}>
        <FontAwesomeIcon icon={icon} size={"lg"} />
      </span>
      <span className={styles.sidebarItemText}>{label}</span>
    </button>
  );
}
