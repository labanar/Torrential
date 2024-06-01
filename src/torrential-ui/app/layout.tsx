"use client";

import { Inter } from "next/font/google";
import { Providers } from "./providers";
import { config } from "@fortawesome/fontawesome-svg-core";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { useCallback, useEffect, useState } from "react";
import SignalRService from "./signalRService";
import styles from "./layout.module.css";
import { useRouter } from "next/navigation";
import {
  Box,
  ColorModeScript,
  Divider,
  Input,
  Modal,
  ModalBody,
  ModalCloseButton,
  ModalContent,
  ModalFooter,
  ModalHeader,
  ModalOverlay,
  SlideFade,
  Text,
  useDisclosure,
} from "@chakra-ui/react";
import ToastNotifications from "@/components/ToastNotification";
import {
  IconDefinition,
  faGear,
  faL,
  faPeopleGroup,
  faPlug,
  faUmbrella,
  faUpDown,
  faUsers,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import next from "next";
import classNames from "classnames";
import Alfred from "@/components/Alfred";
config.autoAddCss = false;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  useEffect(() => {
    const signalRService = new SignalRService(
      `${process.env.NEXT_PUBLIC_API_BASE_URL}/torrents/hub`
    );
    signalRService.startConnection();

    return () => {
      signalRService.stopConnection();
    };
  }, []);

  const { toggleScope, enableScope, disableScope } = useHotkeysContext();
  const [isOpen, setSearchOpen] = useState(false);

  const onToggle = () => {
    if (isOpen) {
      // disableScope("search");
      setSearchOpen(false);
    } else {
      // enableScope("search");
      setSearchOpen(true);
    }
  };

  useHotkeys(
    "mod+ ",
    onToggle,
    {
      scopes: ["global"],
      enableOnFormTags: ["input", "textarea", "select"],
    },
    [onToggle]
  );

  useHotkeys(
    "esc",
    () => {
      setSearchOpen(false);
    },
    {
      scopes: ["global"],
      enableOnFormTags: ["input", "textarea", "select"],
    },
    [setSearchOpen]
  );

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
            <Alfred isOpen={isOpen} close={() => setSearchOpen(false)} />
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
