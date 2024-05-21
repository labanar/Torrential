"use client";

import { Inter } from "next/font/google";
import { Providers } from "./providers";
import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useEffect } from "react";
import SignalRService from "./signalRService";
import styles from "./layout.module.css";
import { useRouter } from "next/navigation";
import { Box, ColorModeScript, Divider, Text } from "@chakra-ui/react";
import ToastNotifications from "@/components/ToastNotification";
import {
  IconDefinition,
  faGear,
  faPlug,
  faUmbrella,
  faUpDown,
  faUsers,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
config.autoAddCss = false;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  useEffect(() => {
    const signalRService = new SignalRService(
      "http://localhost:5142/torrents/hub"
    );
    signalRService.startConnection();

    return () => {
      signalRService.stopConnection();
    };
  }, []);

  // const { colorMode, toggleColorMode } = useColorMode();

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
        <ColorModeScript initialColorMode={"system"} />
        <Providers>
          <div className={styles.root}>
            <SideBar />
            <ToastNotifications />
            <div className={styles.divider}>
              <Divider orientation="vertical" />
            </div>
            <div id="main" className={styles.main}>
              {children}
            </div>
          </div>
        </Providers>
      </body>
    </html>
  );
}

function SideBar() {
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
    </div>
  );
}

interface SideBarItemProps {
  label: string;
  linksTo: string;
  icon: IconDefinition;
}

function SideBarItem({ label, linksTo, icon }: SideBarItemProps) {
  const router = useRouter();

  return (
    <div className={styles.sidebarItem} onClick={() => router.push(linksTo)}>
      <Box width="24px" textAlign="center">
        <FontAwesomeIcon icon={icon} size={"lg"} />
      </Box>
      <Text fontSize={"md"} textAlign={"right"} flexGrow={1}>
        {label}
      </Text>
    </div>
  );
}
