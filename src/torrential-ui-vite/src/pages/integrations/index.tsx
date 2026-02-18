import { useCallback, useEffect, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { faCheck } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import { FormInput } from "../../components/Form/FormInput";
import {
  fetchIntegrationsSettings,
  saveIntegrationsSettings,
} from "../../services/api";
import type { IntegrationsSettings } from "../../types";
import Layout from "../layout";
import styles from "./integrations.module.css";

interface IntegrationsFormValues {
  slackEnabled: boolean;
  slackWebhookUrl: string;
  slackMessageTemplate: string;
  slackTriggerDownloadComplete: boolean;
  discordEnabled: boolean;
  discordWebhookUrl: string;
  discordMessageTemplate: string;
  discordTriggerDownloadComplete: boolean;
  commandHookEnabled: boolean;
  commandTemplate: string;
  commandWorkingDirectory: string;
  commandTriggerDownloadComplete: boolean;
}

const defaultValues: IntegrationsFormValues = {
  slackEnabled: false,
  slackWebhookUrl: "",
  slackMessageTemplate: "",
  slackTriggerDownloadComplete: true,
  discordEnabled: false,
  discordWebhookUrl: "",
  discordMessageTemplate: "",
  discordTriggerDownloadComplete: true,
  commandHookEnabled: false,
  commandTemplate: "",
  commandWorkingDirectory: "",
  commandTriggerDownloadComplete: true,
};

export default function IntegrationsPage() {
  const {
    control,
    formState: { isDirty },
    reset,
  } = useForm<IntegrationsFormValues>({ defaultValues });

  const values = useWatch({ control }) as IntegrationsFormValues;

  const fetchSettings = useCallback(async () => {
    try {
      const data = await fetchIntegrationsSettings();
      reset(toFormValues(data));
    } catch (error) {
      console.error("Failed to fetch integrations settings", error);
    }
  }, [reset]);

  const saveSettings = useCallback(
    async (formValues: IntegrationsFormValues) => {
      try {
        const success = await saveIntegrationsSettings(formValues);
        if (!success) {
          console.error("Failed to save integrations settings");
          return;
        }

        reset(formValues);
      } catch (error) {
        console.error("Failed to save integrations settings", error);
      }
    },
    [reset]
  );

  useEffect(() => {
    fetchSettings();
  }, [fetchSettings]);

  return (
    <Layout>
      <div className={`${styles.integrationsRoot} page-shell`}>
        <div className={styles.pageHeader}>
          <h1 className={styles.pageTitle}>Integrations</h1>
          <Button
            className={styles.saveButton}
            disabled={!isDirty}
            aria-label="Save integrations settings"
            onClick={() => saveSettings(values)}
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
          text="Enable Slack integration"
          className={styles.integrationCheckbox}
        />
        <RowComponent label="Webhook URL">
          <FormInput
            fieldName="slackWebhookUrl"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <RowComponent label="Message Template">
          <FormInput
            fieldName="slackMessageTemplate"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <FormCheckbox
          control={control}
          fieldName="slackTriggerDownloadComplete"
          text="Trigger on download complete"
          className={styles.triggerCheckbox}
        />

        <Separator />

        <SectionHeader name="Discord" />
        <FormCheckbox
          control={control}
          fieldName="discordEnabled"
          text="Enable Discord integration"
          className={styles.integrationCheckbox}
        />
        <RowComponent label="Webhook URL">
          <FormInput
            fieldName="discordWebhookUrl"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <RowComponent label="Message Template">
          <FormInput
            fieldName="discordMessageTemplate"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <FormCheckbox
          control={control}
          fieldName="discordTriggerDownloadComplete"
          text="Trigger on download complete"
          className={styles.triggerCheckbox}
        />

        <Separator />

        <SectionHeader name="Command Hook" />
        <FormCheckbox
          control={control}
          fieldName="commandHookEnabled"
          text="Enable command hook"
          className={styles.integrationCheckbox}
        />
        <RowComponent label="Command Template">
          <FormInput
            fieldName="commandTemplate"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <RowComponent label="Working Directory">
          <FormInput
            fieldName="commandWorkingDirectory"
            control={control}
            className={styles.textInput}
          />
        </RowComponent>
        <FormCheckbox
          control={control}
          fieldName="commandTriggerDownloadComplete"
          text="Trigger on download complete"
          className={styles.triggerCheckbox}
        />
      </div>
    </Layout>
  );
}

function toFormValues(data: IntegrationsSettings): IntegrationsFormValues {
  return {
    slackEnabled: data.slackEnabled,
    slackWebhookUrl: data.slackWebhookUrl,
    slackMessageTemplate: data.slackMessageTemplate,
    slackTriggerDownloadComplete: data.slackTriggerDownloadComplete,
    discordEnabled: data.discordEnabled,
    discordWebhookUrl: data.discordWebhookUrl,
    discordMessageTemplate: data.discordMessageTemplate,
    discordTriggerDownloadComplete: data.discordTriggerDownloadComplete,
    commandHookEnabled: data.commandHookEnabled,
    commandTemplate: data.commandTemplate,
    commandWorkingDirectory: data.commandWorkingDirectory ?? "",
    commandTriggerDownloadComplete: data.commandTriggerDownloadComplete,
  };
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
