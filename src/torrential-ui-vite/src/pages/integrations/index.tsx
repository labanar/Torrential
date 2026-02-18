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

interface SlackSettings {
  slackEnabled: boolean;
  slackWebhookUrl: string;
}

interface DiscordSettings {
  discordEnabled: boolean;
  discordWebhookUrl: string;
}

export default function IntegrationsPage() {
  return (
    <Layout>
      <IntegrationsSettings />
    </Layout>
  );
}

function IntegrationsSettings() {
  const {
    control: slackControl,
    formState: { isDirty: isSlackDirty },
    reset: resetSlack,
  } = useForm<SlackSettings>({
    defaultValues: {
      slackEnabled: false,
      slackWebhookUrl: "",
    },
  });
  const slackValues = useWatch({ control: slackControl }) as SlackSettings;

  const {
    control: discordControl,
    formState: { isDirty: isDiscordDirty },
    reset: resetDiscord,
  } = useForm<DiscordSettings>({
    defaultValues: {
      discordEnabled: false,
      discordWebhookUrl: "",
    },
  });
  const discordValues = useWatch({ control: discordControl }) as DiscordSettings;

  const fetchSettings = useCallback(async () => {
    try {
      const settings = await fetchIntegrationSettings();
      resetSlack({
        slackEnabled: settings.slackEnabled,
        slackWebhookUrl: settings.slackWebhookUrl,
      });
      resetDiscord({
        discordEnabled: settings.discordEnabled,
        discordWebhookUrl: settings.discordWebhookUrl,
      });
    } catch (error) {
      console.error("Failed to fetch integration settings", error);
    }
  }, [resetSlack, resetDiscord]);

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  const handleSave = useCallback(async () => {
    const payload: IntegrationSettingsApiModel = {
      slackEnabled: slackValues.slackEnabled,
      slackWebhookUrl: slackValues.slackWebhookUrl,
      discordEnabled: discordValues.discordEnabled,
      discordWebhookUrl: discordValues.discordWebhookUrl,
    };

    try {
      await saveIntegrationSettings(payload);
      if (isSlackDirty) resetSlack(slackValues);
      if (isDiscordDirty) resetDiscord(discordValues);
    } catch (error) {
      console.error("Failed to save integration settings", error);
    }
  }, [
    slackValues,
    discordValues,
    isSlackDirty,
    isDiscordDirty,
    resetSlack,
    resetDiscord,
  ]);

  return (
    <div className={`${styles.integrationsRoot} page-shell`}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>Integrations</h1>
        <Button
          className={styles.saveButton}
          disabled={!isSlackDirty && !isDiscordDirty}
          aria-label="Save integration settings"
          onClick={handleSave}
          type="button"
        >
          <FontAwesomeIcon icon={faCheck} />
          <span className={styles.saveButtonLabel}>Save</span>
        </Button>
      </div>
      <Separator />

      <SectionHeader name="Slack" />
      <FormCheckbox
        control={slackControl}
        fieldName="slackEnabled"
        text="Enable Slack notifications"
        className={styles.enabledCheckbox}
      />
      <RowComponent label="Webhook URL">
        <FormInput
          fieldName="slackWebhookUrl"
          control={slackControl}
          className={styles.webhookInput}
        />
      </RowComponent>

      <Separator />

      <SectionHeader name="Discord" />
      <FormCheckbox
        control={discordControl}
        fieldName="discordEnabled"
        text="Enable Discord notifications"
        className={styles.enabledCheckbox}
      />
      <RowComponent label="Webhook URL">
        <FormInput
          fieldName="discordWebhookUrl"
          control={discordControl}
          className={styles.webhookInput}
        />
      </RowComponent>
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
