"use client";

import { Divider, Grid, GridItem, IconButton, Text } from "@chakra-ui/react";
import { faCheck } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useForm, useWatch } from "react-hook-form";
import styles from "./settings.module.css";
import { useCallback, useEffect } from "react";
import { FormInput } from "../../components/Form/FormInput";
import { FormNumericInput } from "../../components/Form/FormNumericInput";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import Layout from "../layout";

export default function SettingsPage() {
  return (
    <Layout>
      <GeneralSettings />
    </Layout>
  );
}

function GeneralSettings() {
  const {
    control: fileSettingsControl,
    formState: { isDirty: isFileSettingsDirty },
    reset: resetFileSettings,
  } = useForm({
    defaultValues: {
      downloadPath: "",
      completedPath: "",
    },
  });
  const fileSettingsValues = useWatch({ control: fileSettingsControl });
  const fetchFilesettings = useCallback(async () => {
    try {
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/settings/file`
      );
      const json = await response.json();
      console.log(json);
      const { downloadPath, completedPath } = json.data;
      resetFileSettings({ downloadPath, completedPath });
    } catch {}
  }, [resetFileSettings]);
  const saveFileSettings = useCallback(async (values: any) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/file`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch {}
  }, []);

  const {
    control: connectionSettingsControl,
    formState: { isDirty: isConnectionSettingsDirty },
    reset: resetConnectionSettings,
  } = useForm({
    defaultValues: {
      maxConnectionsPerTorrent: "",
      maxConnectionsGlobal: "",
      maxHalfOpenConnections: "",
    },
  });
  const connectionSettingsValues = useWatch({
    control: connectionSettingsControl,
  });
  const fetchConnectionSettings = useCallback(async () => {
    try {
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/settings/connection`
      );
      const json = await response.json();
      console.log(json);
      const {
        maxConnectionsGlobal,
        maxConnectionsPerTorrent,
        maxHalfOpenConnections,
      } = json.data;
      resetConnectionSettings({
        maxConnectionsGlobal: `${maxConnectionsGlobal}`,
        maxConnectionsPerTorrent: `${maxConnectionsPerTorrent}`,
        maxHalfOpenConnections: `${maxHalfOpenConnections}`,
      });
    } catch {}
  }, [resetConnectionSettings]);
  const saveConnectionSettings = useCallback(async (values: any) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/connection`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch {}
  }, []);

  const {
    control: tcpListenerSettingsControl,
    formState: { isDirty: isTcpListenerSettingsDirty },
    reset: resetTcpListenerSettings,
  } = useForm({
    defaultValues: {
      port: "53123",
      enabled: true,
    },
  });
  const tcpListenerSettingsValue = useWatch({
    control: tcpListenerSettingsControl,
  });
  const fetchTcpListenerSettings = useCallback(async () => {
    try {
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/settings/tcp`
      );
      const json = await response.json();
      console.log(json);
      const { enabled, port } = json.data;
      resetTcpListenerSettings({
        enabled,
        port: `${port}`,
      });
    } catch {}
  }, [resetTcpListenerSettings]);
  const saveTcpSettings = useCallback(async (values: any) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/tcp`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch {}
  }, []);

  useEffect(() => {
    fetchFilesettings();
    fetchTcpListenerSettings();
    fetchConnectionSettings();
  }, []);

  return (
    <>
      <div
        style={{
          padding: "1em",
          flexGrow: 1,
          display: "flex",
          flexDirection: "column",
          gap: "16px",
          alignItems: "center",
        }}
      >
        <Text alignSelf={"flex-start"} fontSize={30}>
          Settings
        </Text>
        <Divider />
        <SectionHeader name="Files" />

        <RowComponent label="Download Path">
          <FormInput fieldName="downloadPath" control={fileSettingsControl} />
        </RowComponent>
        <RowComponent label="Completed Path">
          <FormInput fieldName="completedPath" control={fileSettingsControl} />
        </RowComponent>

        <Divider />
        <SectionHeader name="Connections" />

        <RowComponent label="Max connections (per torrent)">
          <FormNumericInput
            min={0}
            control={connectionSettingsControl}
            fieldName="maxConnectionsPerTorrent"
            className={styles.connectionNumericInput}
          />
        </RowComponent>

        <RowComponent label="Max connections (Global)">
          <FormNumericInput
            min={0}
            control={connectionSettingsControl}
            fieldName="maxConnectionsGlobal"
            className={styles.connectionNumericInput}
          />
        </RowComponent>

        <RowComponent label="Max Half-open connections">
          <FormNumericInput
            min={0}
            control={connectionSettingsControl}
            fieldName="maxHalfOpenConnections"
            className={styles.connectionNumericInput}
          />
        </RowComponent>

        <Divider />
        <SectionHeader name="Inbound Connections" />

        <FormCheckbox
          control={tcpListenerSettingsControl}
          fieldName="enabled"
          text="Allow inbound connections"
          className={styles.tcpInboundCheckbox}
        />

        <RowComponent label="Port">
          <FormNumericInput
            min={0}
            control={tcpListenerSettingsControl}
            fieldName="port"
            className={styles.portInput}
          />
        </RowComponent>
      </div>
      <IconButton
        position={"absolute"}
        bottom={0}
        right={0}
        mr={8}
        mb={8}
        isRound={true}
        variant="solid"
        colorScheme="green"
        aria-label="Done"
        fontSize="30px"
        size={"lg"}
        isDisabled={
          !isFileSettingsDirty &&
          !isConnectionSettingsDirty &&
          !isTcpListenerSettingsDirty
        }
        onClick={() => {
          if (isFileSettingsDirty) {
            console.log("Saving file settings");
            console.log(fileSettingsValues);
            saveFileSettings(fileSettingsValues);
            resetFileSettings(fileSettingsValues);
          }
          if (isConnectionSettingsDirty) {
            console.log("Saving connection settings");
            console.log(connectionSettingsValues);
            saveConnectionSettings(connectionSettingsValues);
            resetConnectionSettings(connectionSettingsValues);
          }
          if (isTcpListenerSettingsDirty) {
            console.log("Saving TCP Listener settings");
            console.log(tcpListenerSettingsValue);
            saveTcpSettings(tcpListenerSettingsValue);
            resetTcpListenerSettings(tcpListenerSettingsValue);
          }
        }}
        icon={<FontAwesomeIcon icon={faCheck} />}
      />
    </>
  );
}

interface SectionHeaderProps {
  name: string;
}
function SectionHeader({ name }: SectionHeaderProps) {
  return (
    <Text alignSelf={"flex-start"} fontSize={20} fontWeight={500} pb={4}>
      {name}
    </Text>
  );
}

interface RowInputProps {
  label: string;
  children: React.ReactNode;
}

const RowComponent: React.FC<RowInputProps> = ({ label, children }) => {
  return (
    <Grid templateColumns="repeat(2, 1fr)" alignItems={"center"} gap={8}>
      <GridItem>
        <Text align={"right"}>{label}</Text>
      </GridItem>
      {children}
    </Grid>
  );
};
