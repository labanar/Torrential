"use client";

import { faCheck } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useForm, useWatch } from "react-hook-form";
import styles from "./settings.module.css";
import { useCallback, useEffect, type ReactNode } from "react";
import { FormInput } from "../../components/Form/FormInput";
import { FormNumericInput } from "../../components/Form/FormNumericInput";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
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
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/file`);
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
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/tcp`);
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
  }, [fetchConnectionSettings, fetchFilesettings, fetchTcpListenerSettings]);

  return (
    <>
      <div className={styles.settingsRoot}>
        <h1 className={styles.pageTitle}>Settings</h1>
        <Separator />
        <SectionHeader name="Files" />

        <RowComponent label="Download Path">
          <FormInput fieldName="downloadPath" control={fileSettingsControl} />
        </RowComponent>
        <RowComponent label="Completed Path">
          <FormInput fieldName="completedPath" control={fileSettingsControl} />
        </RowComponent>

        <Separator />
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

        <Separator />
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
      <Button
        className={styles.saveButton}
        size="icon"
        disabled={
          !isFileSettingsDirty && !isConnectionSettingsDirty && !isTcpListenerSettingsDirty
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
        type="button"
      >
        <FontAwesomeIcon icon={faCheck} />
      </Button>
    </>
  );
}

function SectionHeader({ name }: { name: string }) {
  return <h2 className={styles.sectionTitle}>{name}</h2>;
}

interface RowInputProps {
  label: string;
  children: ReactNode;
}

const RowComponent = ({ label, children }: RowInputProps) => {
  return (
    <div className={styles.settingRow}>
      <p className={styles.settingLabel}>{label}</p>
      <div>{children}</div>
    </div>
  );
};
