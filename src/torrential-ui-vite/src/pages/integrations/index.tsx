"use client";

import { faCheck } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useForm, useWatch } from "react-hook-form";
import styles from "./integrations.module.css";
import { useCallback, useEffect, type ReactNode } from "react";
import { FormInput } from "../../components/Form/FormInput";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import Layout from "../layout";
import {
  fetchIntegrationSettings,
  saveIntegrationSettings,
  type IntegrationSettingsApiModel,
} from "../../services/api";

interface IntegrationFormValues {
  slackEnabled: boolean;
  slackWebhookUrl: string;
  slackOnTorrentComplete: boolean;
  discordEnabled: boolean;
  discordWebhookUrl: string;
  discordOnTorrentComplete: boolean;
  commandEnabled: boolean;
  command: string;
  commandOnTorrentComplete: boolean;
}

const defaultValues: IntegrationFormValues = {
  slackEnabled: false,
  slackWebhookUrl: "",
  slackOnTorrentComplete: false,
  discordEnabled: false,
  discordWebhookUrl: "",
  discordOnTorrentComplete: false,
  commandEnabled: false,
  command: "",
  commandOnTorrentComplete: false,
};

export default function IntegrationsPage() {
  return (
    <Layout>
      <IntegrationsSettings />
    </Layout>
  );
}

function IntegrationsSettings() {
  const {
    control,
    formState: { isDirty },
    reset,
  } = useForm<IntegrationFormValues>({ defaultValues });

  const values = useWatch({ control }) as IntegrationFormValues;

  const fetchSettings = useCallback(async () => {
    try {
      const data = await fetchIntegrationSettings();
      reset(data);
    } catch (error) {
      console.error("Failed to fetch integration settings", error);
    }
  }, [reset]);

  const saveSettings = useCallback(async (vals: IntegrationFormValues) => {
    try {
      const payload: IntegrationSettingsApiModel = { ...vals };
      await saveIntegrationSettings(payload);
    } catch (error) {
      console.error("Failed to save integration settings", error);
    }
  }, []);

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  return (
    <div className={`${styles.integrationsRoot} page-shell`}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>Integrations</h1>
        <Button
          className={styles.saveButton}
          disabled={!isDirty}
          aria-label="Save integrations"
          onClick={() => {
            saveSettings(values);
            reset(values);
          }}
          type="button"
        >
          <FontAwesomeIcon icon={faCheck} />
          <span className={styles.saveButtonLabel}>Save</span>
        </Button>
      </div>

      <Separator />
      <SectionHeader name="Slack" />

      <FormCheckbox
        control={control}
        fieldName="slackEnabled"
        text="Enable Slack notifications"
        className={styles.enableCheckbox}
      />

      <RowComponent label="Webhook URL">
        <FormInput
          fieldName="slackWebhookUrl"
          control={control}
          className={styles.webhookInput}
        />
      </RowComponent>

      <FormCheckbox
        control={control}
        fieldName="slackOnTorrentComplete"
        text="Notify on torrent complete"
        className={styles.triggerCheckbox}
      />

      <Separator />
      <SectionHeader name="Discord" />

      <FormCheckbox
        control={control}
        fieldName="discordEnabled"
        text="Enable Discord notifications"
        className={styles.enableCheckbox}
      />

      <RowComponent label="Webhook URL">
        <FormInput
          fieldName="discordWebhookUrl"
          control={control}
          className={styles.webhookInput}
        />
      </RowComponent>

      <FormCheckbox
        control={control}
        fieldName="discordOnTorrentComplete"
        text="Notify on torrent complete"
        className={styles.triggerCheckbox}
      />

      <Separator />
      <SectionHeader name="Command" />

      <FormCheckbox
        control={control}
        fieldName="commandEnabled"
        text="Enable command execution"
        className={styles.enableCheckbox}
      />

      <RowComponent label="Command">
        <FormInput
          fieldName="command"
          control={control}
          className={styles.commandInput}
        />
      </RowComponent>

      <FormCheckbox
        control={control}
        fieldName="commandOnTorrentComplete"
        text="Run on torrent complete"
        className={styles.triggerCheckbox}
      />
    </div>
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
      <div className={styles.settingControl}>{children}</div>
    </div>
  );
};
