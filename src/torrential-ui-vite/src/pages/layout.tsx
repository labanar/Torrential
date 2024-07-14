import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useEffect, useMemo, useState } from "react";
import styles from "./layout.module.css";
import { Box, Divider, IconButton, Text, useColorMode } from "@chakra-ui/react";
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
import { useHotkeys } from "react-hotkeys-hook";
import { useNavigate } from "react-router-dom";
import Alfred from "../components/Alfred/alfred";
import SignalRService from "../services/signalR";
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
    <html lang="en" style={{ height: "100%", margin: 0 }}>
      <body
        style={{
          height: "100%",
          margin: 0,
          display: "flex",
          flexDirection: "column",
        }}
      >
        <div className={styles.root}>
          <SideBar />
          {alfred}
          <div className={styles.divider}>
            <Divider orientation="vertical" />
          </div>
          <div id="main" className={styles.main}>
            {children}
          </div>
        </div>
      </body>
    </html>
  );
}

function SideBar() {
  const { colorMode, toggleColorMode } = useColorMode();

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
      <Text className={styles.sidebarTitle}>TORRENTIAL</Text>
      <SideBarItem label="TORRENTS" linksTo="/" icon={faUpDown} />
      <SideBarItem label="PEERS" linksTo="/peers" icon={faUsers} />
      <SideBarItem label="INTEGRATIONS" linksTo="/integrations" icon={faPlug} />
      <SideBarItem label="SETTINGS" linksTo="/settings" icon={faGear} />

      <div
        style={{
          flex: 1,
          alignContent: "flex-end",
        }}
      >
        <IconButton
          icon={
            <Box width={"24px"} height={"24px"}>
              <FontAwesomeIcon
                icon={colorMode === "dark" ? faSun : faMoon}
                fontSize={24}
              />
            </Box>
          }
          onClick={toggleColorMode}
          aria-label={""}
          variant={"ghost"}
        />
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
    <div className={styles.sidebarItem} onClick={() => navigate(linksTo)}>
      <Box width="24px" textAlign="center">
        <FontAwesomeIcon icon={icon} size={"lg"} />
      </Box>
      <Text fontSize={"md"} textAlign={"right"} flexGrow={1}>
        {label}
      </Text>
    </div>
  );
}
