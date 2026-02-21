import { faHashtag, faGamepad, faTerminal, faPlus, faPen, faTrash } from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useCallback, useEffect, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import styles from "./integrations.module.css";
import { FormInput } from "../../components/Form/FormInput";
import { FormCheckbox } from "../../components/Form/FormCheckbox";
import { Separator } from "@/components/ui/separator";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from "@/components/ui/dialog";
import Layout from "../layout";
import {
  type IntegrationApiModel,
  fetchIntegrations,
  createIntegration,
  updateIntegration,
  deleteIntegration,
} from "../../services/api";

const INTEGRATION_TYPES = [
  { value: "Slack", label: "Slack", icon: faHashtag },
  { value: "Discord", label: "Discord", icon: faGamepad },
  { value: "Exec", label: "Exec", icon: faTerminal },
] as const;

const TRIGGER_EVENTS = [
  { value: "torrent_complete", label: "Download Complete" },
  { value: "torrent_added", label: "Torrent Added" },
  { value: "torrent_started", label: "Torrent Started" },
  { value: "torrent_stopped", label: "Torrent Stopped" },
] as const;

function getTypeIcon(type: string) {
  const found = INTEGRATION_TYPES.find((t) => t.value === type);
  return found?.icon ?? faHashtag;
}

function getTriggerEventLabel(event: string) {
  const found = TRIGGER_EVENTS.find((e) => e.value === event);
  return found?.label ?? event;
}

interface IntegrationFormValues {
  name: string;
  type: string;
  triggerEvent: string;
  enabled: boolean;
  webhookUrl: string;
  command: string;
}

function serializeConfiguration(type: string, values: IntegrationFormValues): string {
  if (type === "Slack" || type === "Discord") {
    return JSON.stringify({ webhookUrl: values.webhookUrl });
  }
  if (type === "Exec") {
    return JSON.stringify({ command: values.command });
  }
  return "{}";
}

function parseConfiguration(type: string, configJson: string): { webhookUrl: string; command: string } {
  try {
    const parsed = JSON.parse(configJson);
    if (type === "Slack" || type === "Discord") {
      return { webhookUrl: parsed.webhookUrl ?? "", command: "" };
    }
    if (type === "Exec") {
      return { webhookUrl: "", command: parsed.command ?? "" };
    }
  } catch {
    // ignore parse errors
  }
  return { webhookUrl: "", command: "" };
}

const DEFAULT_FORM_VALUES: IntegrationFormValues = {
  name: "",
  type: "Slack",
  triggerEvent: "torrent_complete",
  enabled: true,
  webhookUrl: "",
  command: "",
};

export default function IntegrationsPage() {
  return (
    <Layout>
      <IntegrationsContent />
    </Layout>
  );
}

function IntegrationsContent() {
  const [integrations, setIntegrations] = useState<IntegrationApiModel[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const { control, handleSubmit, reset, watch } = useForm<IntegrationFormValues>({
    defaultValues: DEFAULT_FORM_VALUES,
  });

  const selectedType = watch("type");

  const loadIntegrations = useCallback(async () => {
    try {
      const data = await fetchIntegrations();
      setIntegrations(data);
    } catch (error) {
      console.error("Failed to fetch integrations", error);
    }
  }, []);

  useEffect(() => {
    loadIntegrations();
  }, [loadIntegrations]);

  const openAddDialog = useCallback(() => {
    setEditingId(null);
    reset(DEFAULT_FORM_VALUES);
    setDialogOpen(true);
  }, [reset]);

  const openEditDialog = useCallback(
    (integration: IntegrationApiModel) => {
      setEditingId(integration.id);
      const config = parseConfiguration(integration.type, integration.configuration);
      reset({
        name: integration.name,
        type: integration.type,
        triggerEvent: integration.triggerEvent,
        enabled: integration.enabled,
        webhookUrl: config.webhookUrl,
        command: config.command,
      });
      setDialogOpen(true);
    },
    [reset]
  );

  const onSubmit = useCallback(
    async (values: IntegrationFormValues) => {
      const payload = {
        name: values.name,
        type: values.type,
        triggerEvent: values.triggerEvent,
        enabled: values.enabled,
        configuration: serializeConfiguration(values.type, values),
      };
      try {
        if (editingId) {
          await updateIntegration(editingId, payload);
        } else {
          await createIntegration(payload);
        }
        setDialogOpen(false);
        loadIntegrations();
      } catch (error) {
        console.error("Failed to save integration", error);
      }
    },
    [editingId, loadIntegrations]
  );

  const handleDelete = useCallback(
    async (id: string) => {
      try {
        await deleteIntegration(id);
        loadIntegrations();
      } catch (error) {
        console.error("Failed to delete integration", error);
      }
    },
    [loadIntegrations]
  );

  const handleToggleEnabled = useCallback(
    async (integration: IntegrationApiModel) => {
      try {
        await updateIntegration(integration.id, {
          name: integration.name,
          type: integration.type,
          triggerEvent: integration.triggerEvent,
          enabled: !integration.enabled,
          configuration: integration.configuration,
        });
        loadIntegrations();
      } catch (error) {
        console.error("Failed to toggle integration", error);
      }
    },
    [loadIntegrations]
  );

  return (
    <div className={`${styles.integrationsRoot} page-shell`}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>Integrations</h1>
        <Button className={styles.addButton} onClick={openAddDialog} type="button">
          <FontAwesomeIcon icon={faPlus} />
          <span className={styles.addButtonLabel}>Add Integration</span>
        </Button>
      </div>
      <Separator />

      {integrations.length === 0 ? (
        <div className={styles.emptyState}>
          <p>No integrations configured.</p>
        </div>
      ) : (
        <div className={styles.integrationsList}>
          {integrations.map((integration) => (
            <div key={integration.id} className={styles.integrationCard}>
              <div className={styles.integrationIcon}>
                <FontAwesomeIcon icon={getTypeIcon(integration.type)} />
              </div>
              <div className={styles.integrationInfo}>
                <p className={styles.integrationName}>{integration.name}</p>
                <p className={styles.integrationMeta}>
                  {integration.type} &middot; {getTriggerEventLabel(integration.triggerEvent)}
                  {!integration.enabled && " \u00B7 Disabled"}
                </p>
              </div>
              <div className={styles.integrationActions}>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => handleToggleEnabled(integration)}
                  aria-label={integration.enabled ? "Disable integration" : "Enable integration"}
                  style={{ opacity: integration.enabled ? 1 : 0.5 }}
                >
                  <span
                    style={{
                      width: "2rem",
                      height: "1.15rem",
                      borderRadius: "999px",
                      background: integration.enabled
                        ? "hsl(var(--primary))"
                        : "hsl(var(--muted))",
                      position: "relative",
                      display: "inline-block",
                      transition: "background 0.2s",
                    }}
                  >
                    <span
                      style={{
                        position: "absolute",
                        top: "0.1rem",
                        left: integration.enabled ? "calc(100% - 1.05rem)" : "0.1rem",
                        width: "0.95rem",
                        height: "0.95rem",
                        borderRadius: "50%",
                        background: "hsl(var(--background))",
                        transition: "left 0.2s",
                      }}
                    />
                  </span>
                </Button>
                <Button variant="ghost" size="icon" onClick={() => openEditDialog(integration)} aria-label="Edit integration">
                  <FontAwesomeIcon icon={faPen} />
                </Button>
                <Button variant="ghost" size="icon" onClick={() => handleDelete(integration.id)} aria-label="Delete integration">
                  <FontAwesomeIcon icon={faTrash} />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingId ? "Edit Integration" : "Add Integration"}</DialogTitle>
            <DialogDescription>
              {editingId
                ? "Update the integration settings below."
                : "Configure a new integration to respond to torrent events."}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={handleSubmit(onSubmit)} className={styles.dialogForm}>
            <div className={styles.formField}>
              <label className={styles.formLabel}>Name</label>
              <FormInput fieldName="name" control={control} />
            </div>

            <div className={styles.formField}>
              <label className={styles.formLabel}>Type</label>
              <Controller
                name="type"
                control={control}
                render={({ field }) => (
                  <select {...field} className={styles.formSelect}>
                    {INTEGRATION_TYPES.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                )}
              />
            </div>

            <div className={styles.formField}>
              <label className={styles.formLabel}>Trigger Event</label>
              <Controller
                name="triggerEvent"
                control={control}
                render={({ field }) => (
                  <select {...field} className={styles.formSelect}>
                    {TRIGGER_EVENTS.map((e) => (
                      <option key={e.value} value={e.value}>
                        {e.label}
                      </option>
                    ))}
                  </select>
                )}
              />
            </div>

            <FormCheckbox control={control} fieldName="enabled" text="Enabled" />

            {(selectedType === "Slack" || selectedType === "Discord") && (
              <div className={styles.formField}>
                <label className={styles.formLabel}>Webhook URL</label>
                <FormInput fieldName="webhookUrl" control={control} />
              </div>
            )}

            {selectedType === "Exec" && (
              <div className={styles.formField}>
                <label className={styles.formLabel}>Command</label>
                <FormInput fieldName="command" control={control} />
              </div>
            )}

            <DialogFooter>
              <Button type="submit">{editingId ? "Save Changes" : "Create"}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
