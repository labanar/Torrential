"use client";

import { faCheck } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useForm, useWatch } from "react-hook-form";
import { useCallback, useEffect, type ReactNode } from "react";
import { FormInput } from "../../components/Form/FormInput";
import { FormNumericInput } from "../../components/Form/FormNumericInput";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import Layout from "../layout";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";

interface FileSettings {
  downloadPath: string;
  completedPath: string;
}

interface ConnectionSettings {
  maxConnectionsPerTorrent: string;
  maxConnectionsGlobal: string;
  maxHalfOpenConnections: string;
}

interface TcpListenerSettings {
  port: string;
  enabled: boolean;
}

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
  } = useForm<FileSettings>({
    defaultValues: {
      downloadPath: "",
      completedPath: "",
    },
  });
  const fileSettingsValues = useWatch({ control: fileSettingsControl }) as FileSettings;
  const fetchFilesettings = useCallback(async () => {
    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/file`);
      const json = await response.json();
      const { downloadPath, completedPath } = json.data;
      resetFileSettings({ downloadPath, completedPath });
    } catch (error) {
      console.error("Failed to fetch file settings", error);
    }
  }, [resetFileSettings]);
  const saveFileSettings = useCallback(async (values: FileSettings) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/file`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch (error) {
      console.error("Failed to save file settings", error);
    }
  }, []);

  const {
    control: connectionSettingsControl,
    formState: { isDirty: isConnectionSettingsDirty },
    reset: resetConnectionSettings,
  } = useForm<ConnectionSettings>({
    defaultValues: {
      maxConnectionsPerTorrent: "",
      maxConnectionsGlobal: "",
      maxHalfOpenConnections: "",
    },
  });
  const connectionSettingsValues = useWatch({
    control: connectionSettingsControl,
  }) as ConnectionSettings;
  const fetchConnectionSettings = useCallback(async () => {
    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/connection`);
      const json = await response.json();
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
    } catch (error) {
      console.error("Failed to fetch connection settings", error);
    }
  }, [resetConnectionSettings]);
  const saveConnectionSettings = useCallback(async (values: ConnectionSettings) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/connection`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch (error) {
      console.error("Failed to save connection settings", error);
    }
  }, []);

  const {
    control: tcpListenerSettingsControl,
    formState: { isDirty: isTcpListenerSettingsDirty },
    reset: resetTcpListenerSettings,
  } = useForm<TcpListenerSettings>({
    defaultValues: {
      port: "53123",
      enabled: true,
    },
  });
  const tcpListenerSettingsValue = useWatch({
    control: tcpListenerSettingsControl,
  }) as TcpListenerSettings;
  const fetchTcpListenerSettings = useCallback(async () => {
    try {
      const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/tcp`);
      const json = await response.json();
      const { enabled, port } = json.data;
      resetTcpListenerSettings({
        enabled,
        port: `${port}`,
      });
    } catch (error) {
      console.error("Failed to fetch TCP listener settings", error);
    }
  }, [resetTcpListenerSettings]);
  const saveTcpSettings = useCallback(async (values: TcpListenerSettings) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/tcp`, {
        method: "POST",
        body: JSON.stringify(values),
        headers: {
          "Content-Type": "application/json",
        },
      });
    } catch (error) {
      console.error("Failed to save TCP settings", error);
    }
  }, []);

  useEffect(() => {
    fetchFilesettings();
    fetchTcpListenerSettings();
    fetchConnectionSettings();
  }, [fetchConnectionSettings, fetchFilesettings, fetchTcpListenerSettings]);

  return (
    <div className="mx-auto flex h-full w-full max-w-6xl min-h-0 flex-col overflow-auto p-4 md:p-6">
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="text-2xl">Settings</CardTitle>
            <Button
              disabled={!isFileSettingsDirty && !isConnectionSettingsDirty && !isTcpListenerSettingsDirty}
              aria-label="Save settings"
              onClick={() => {
                if (isFileSettingsDirty) {
                  saveFileSettings(fileSettingsValues);
                  resetFileSettings(fileSettingsValues);
                }
                if (isConnectionSettingsDirty) {
                  saveConnectionSettings(connectionSettingsValues);
                  resetConnectionSettings(connectionSettingsValues);
                }
                if (isTcpListenerSettingsDirty) {
                  saveTcpSettings(tcpListenerSettingsValue);
                  resetTcpListenerSettings(tcpListenerSettingsValue);
                }
              }}
              type="button"
            >
              <FontAwesomeIcon icon={faCheck} />
              <span>Save</span>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
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
            <FormNumericInput min={0} control={connectionSettingsControl} fieldName="maxConnectionsPerTorrent" />
          </RowComponent>
          <RowComponent label="Max connections (global)">
            <FormNumericInput min={0} control={connectionSettingsControl} fieldName="maxConnectionsGlobal" />
          </RowComponent>
          <RowComponent label="Max half-open connections">
            <FormNumericInput min={0} control={connectionSettingsControl} fieldName="maxHalfOpenConnections" />
          </RowComponent>

          <Separator />
          <SectionHeader name="Inbound Connections" />
          <FormCheckbox
            control={tcpListenerSettingsControl}
            fieldName="enabled"
            text="Allow inbound connections"
            className="inline-flex min-h-11 items-center gap-2"
          />
          <RowComponent label="Port">
            <FormNumericInput min={0} control={tcpListenerSettingsControl} fieldName="port" />
          </RowComponent>
        </CardContent>
      </Card>
    </div>
  );
}

function SectionHeader({ name }: { name: string }) {
  return <h2 className="text-lg font-semibold">{name}</h2>;
}

interface RowInputProps {
  label: string;
  children: ReactNode;
}

const RowComponent = ({ label, children }: RowInputProps) => {
  return (
    <div className="grid gap-2 md:grid-cols-[220px_1fr] md:items-center">
      <Label className="text-muted-foreground md:text-right">{label}</Label>
      <div>{children}</div>
    </div>
  );
};
