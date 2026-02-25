import { useCallback, useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faPen,
  faTrash,
  faFlask,
  faMagnifyingGlass,
  faDownload,
} from "@fortawesome/free-solid-svg-icons";
import { toast } from "sonner";
import Layout from "../layout";
import { useAppDispatch, useAppSelector } from "../../store";
import {
  setIndexersLoading,
  setIndexers,
  setIndexersError,
  addIndexer,
  updateIndexerInList,
  removeIndexer,
  setSearchLoading,
  setSearchResults,
  setSearchError,
} from "../../store/slices/indexerSlice";
import {
  fetchIndexers as fetchIndexersApi,
  createIndexer as createIndexerApi,
  updateIndexer as updateIndexerApi,
  deleteIndexer as deleteIndexerApi,
  testIndexer as testIndexerApi,
  searchIndexers as searchIndexersApi,
  addTorrentFromUrl,
  type IndexerVm,
  type CreateIndexerRequest,
  type SearchResultVm,
} from "../../services/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from "@/components/ui/dialog";
import { Control, Controller, FieldValues, Path } from "react-hook-form";
import styles from "./integrations.module.css";

export default function IntegrationsPage() {
  return (
    <Layout>
      <Page />
    </Layout>
  );
}

function Page() {
  const dispatch = useAppDispatch();
  const { indexers, indexersLoading, searchResults, searchLoading, searchQuery } =
    useAppSelector((s) => s.indexer);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingIndexer, setEditingIndexer] = useState<IndexerVm | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const loadIndexers = useCallback(async () => {
    dispatch(setIndexersLoading());
    try {
      const data = await fetchIndexersApi();
      dispatch(setIndexers(data));
    } catch (e) {
      dispatch(setIndexersError(e instanceof Error ? e.message : "Failed to load indexers"));
    }
  }, [dispatch]);

  useEffect(() => {
    loadIndexers();
  }, [loadIndexers]);

  const handleTest = useCallback(
    async (id: string) => {
      setTestingId(id);
      try {
        const success = await testIndexerApi(id);
        if (success) {
          toast.success("Connection test passed");
        } else {
          toast.error("Connection test failed");
        }
      } catch {
        toast.error("Connection test failed");
      } finally {
        setTestingId(null);
      }
    },
    []
  );

  const handleDelete = useCallback(
    async (id: string) => {
      setDeletingId(id);
      try {
        const ok = await deleteIndexerApi(id);
        if (ok) {
          dispatch(removeIndexer(id));
          toast.success("Indexer deleted");
        } else {
          toast.error("Failed to delete indexer");
        }
      } catch {
        toast.error("Failed to delete indexer");
      } finally {
        setDeletingId(null);
      }
    },
    [dispatch]
  );

  const openCreate = useCallback(() => {
    setEditingIndexer(null);
    setDialogOpen(true);
  }, []);

  const openEdit = useCallback((indexer: IndexerVm) => {
    setEditingIndexer(indexer);
    setDialogOpen(true);
  }, []);

  const handleSaved = useCallback(
    (indexer: IndexerVm, isNew: boolean) => {
      if (isNew) {
        dispatch(addIndexer(indexer));
      } else {
        dispatch(updateIndexerInList(indexer));
      }
      setDialogOpen(false);
    },
    [dispatch]
  );

  return (
    <div className={`${styles.root} page-shell`}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>Integrations</h1>
        <Button onClick={openCreate} size="sm" aria-label="Add indexer">
          <FontAwesomeIcon icon={faPlus} />
          <span>Add</span>
        </Button>
      </div>

      <Separator />

      <h2 className={styles.sectionTitle}>Indexers</h2>
      <IndexerList
        indexers={indexers}
        loading={indexersLoading}
        testingId={testingId}
        deletingId={deletingId}
        onTest={handleTest}
        onEdit={openEdit}
        onDelete={handleDelete}
      />

      <Separator />

      <h2 className={styles.sectionTitle}>Search</h2>
      <SearchSection
        loading={searchLoading}
        results={searchResults}
        query={searchQuery}
        hasEnabledIndexers={indexers.some((i) => i.enabled)}
        dispatch={dispatch}
      />

      <IndexerFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editing={editingIndexer}
        onSaved={handleSaved}
      />
    </div>
  );
}

// --- Indexer list ---

interface IndexerListProps {
  indexers: IndexerVm[];
  loading: boolean;
  testingId: string | null;
  deletingId: string | null;
  onTest: (id: string) => void;
  onEdit: (indexer: IndexerVm) => void;
  onDelete: (id: string) => void;
}

function IndexerList({
  indexers,
  loading,
  testingId,
  deletingId,
  onTest,
  onEdit,
  onDelete,
}: IndexerListProps) {
  if (loading && indexers.length === 0) {
    return <div className={styles.emptyState}>Loading indexers...</div>;
  }

  if (indexers.length === 0) {
    return <div className={styles.emptyState}>No indexers configured</div>;
  }

  return (
    <div className={styles.indexerList}>
      {indexers.map((indexer) => (
        <div key={indexer.id} className={styles.indexerCard}>
          <div className={styles.indexerInfo}>
            <span className={styles.indexerName}>{indexer.name}</span>
            <span className={styles.indexerMeta}>
              <span>{indexer.type}</span>
              <span>{indexer.baseUrl}</span>
              <span
                className={`${styles.enabledBadge} ${
                  indexer.enabled ? styles.enabledBadgeOn : styles.enabledBadgeOff
                }`}
              >
                {indexer.enabled ? "Enabled" : "Disabled"}
              </span>
            </span>
          </div>
          <div className={styles.indexerActions}>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Test ${indexer.name}`}
              onClick={() => onTest(indexer.id)}
              loading={testingId === indexer.id}
            >
              <FontAwesomeIcon icon={faFlask} />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Edit ${indexer.name}`}
              onClick={() => onEdit(indexer)}
            >
              <FontAwesomeIcon icon={faPen} />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Delete ${indexer.name}`}
              onClick={() => onDelete(indexer.id)}
              loading={deletingId === indexer.id}
            >
              <FontAwesomeIcon icon={faTrash} />
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}

// --- Search ---

interface SearchSectionProps {
  loading: boolean;
  results: SearchResultVm[];
  query: string;
  hasEnabledIndexers: boolean;
  dispatch: ReturnType<typeof useAppDispatch>;
}

function SearchSection({ loading, results, query, hasEnabledIndexers, dispatch }: SearchSectionProps) {
  const [searchInput, setSearchInput] = useState("");

  const handleSearch = useCallback(async () => {
    const trimmed = searchInput.trim();
    if (!trimmed) return;
    dispatch(setSearchLoading());
    try {
      const data = await searchIndexersApi({ query: trimmed });
      dispatch(setSearchResults({ query: trimmed, results: data }));
    } catch (e) {
      dispatch(setSearchError(e instanceof Error ? e.message : "Search failed"));
      toast.error("Search failed");
    }
  }, [searchInput, dispatch]);

  const handleAddTorrent = useCallback(async (result: SearchResultVm) => {
    if (!result.downloadUrl) {
      toast.error("No download URL available");
      return;
    }
    try {
      await addTorrentFromUrl(result.downloadUrl);
      toast.success(`Added: ${result.title}`);
    } catch {
      toast.error("Failed to add torrent");
    }
  }, []);

  return (
    <div className={styles.searchSection}>
      <div className={styles.searchRow}>
        <Input
          className={styles.searchInput}
          placeholder={hasEnabledIndexers ? "Search torrents..." : "Enable an indexer to search"}
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") handleSearch();
          }}
          disabled={!hasEnabledIndexers}
        />
        <Button
          onClick={handleSearch}
          disabled={!hasEnabledIndexers || !searchInput.trim()}
          loading={loading}
          aria-label="Search"
        >
          <FontAwesomeIcon icon={faMagnifyingGlass} />
          <span>Search</span>
        </Button>
      </div>

      {query && !loading && results.length === 0 && (
        <div className={styles.emptyState}>No results for &quot;{query}&quot;</div>
      )}

      {results.length > 0 && (
        <>
          {/* Desktop table */}
          <div className={styles.resultsWrap}>
            <table className={styles.resultsTable}>
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Size</th>
                  <th>S</th>
                  <th>L</th>
                  <th>Indexer</th>
                  <th>Date</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {results.map((r, i) => (
                  <tr key={`${r.title}-${i}`}>
                    <td className={styles.resultTitle} title={r.title}>
                      {r.title}
                    </td>
                    <td>{formatSize(r.sizeBytes)}</td>
                    <td className={styles.seeders}>{r.seeders}</td>
                    <td className={styles.leechers}>{r.leechers}</td>
                    <td>{r.indexerName ?? "-"}</td>
                    <td>{r.publishDate ? formatDate(r.publishDate) : "-"}</td>
                    <td>
                      {r.downloadUrl && (
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Add ${r.title}`}
                          onClick={() => handleAddTorrent(r)}
                        >
                          <FontAwesomeIcon icon={faDownload} />
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile cards */}
          <div className={styles.resultCards}>
            {results.map((r, i) => (
              <div key={`${r.title}-${i}`} className={styles.resultCard}>
                <div className={styles.cardRow}>
                  <span className={styles.cardLabel}>Title</span>
                  <span className={styles.cardValue}>{r.title}</span>
                </div>
                <div className={styles.cardRow}>
                  <span className={styles.cardLabel}>Size</span>
                  <span className={styles.cardValue}>{formatSize(r.sizeBytes)}</span>
                </div>
                <div className={styles.cardRow}>
                  <span className={styles.cardLabel}>S / L</span>
                  <span className={styles.cardValue}>
                    <span className={styles.seeders}>{r.seeders}</span>
                    {" / "}
                    <span className={styles.leechers}>{r.leechers}</span>
                  </span>
                </div>
                <div className={styles.cardRow}>
                  <span className={styles.cardLabel}>Indexer</span>
                  <span className={styles.cardValue}>{r.indexerName ?? "-"}</span>
                </div>
                {r.metadata && (
                  <div className={styles.metadataRow}>
                    {r.metadata.artworkUrl && (
                      <img
                        src={r.metadata.artworkUrl}
                        alt=""
                        className={styles.metadataPoster}
                      />
                    )}
                    <div className={styles.metadataInfo}>
                      {r.metadata.year && <span>Year: {r.metadata.year}</span>}
                      {r.metadata.genre && <span>Genre: {r.metadata.genre}</span>}
                      {r.metadata.description && <span>{r.metadata.description}</span>}
                    </div>
                  </div>
                )}
                {r.downloadUrl && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleAddTorrent(r)}
                    aria-label={`Add ${r.title}`}
                  >
                    <FontAwesomeIcon icon={faDownload} />
                    <span>Add</span>
                  </Button>
                )}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

// --- Indexer form dialog ---

interface IndexerFormValues {
  name: string;
  type: "Torznab" | "Rss";
  baseUrl: string;
  authMode: "None" | "ApiKey" | "BasicAuth";
  apiKey: string;
  username: string;
  password: string;
  enabled: boolean;
}

interface IndexerFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing: IndexerVm | null;
  onSaved: (indexer: IndexerVm, isNew: boolean) => void;
}

function IndexerFormDialog({ open, onOpenChange, editing, onSaved }: IndexerFormDialogProps) {
  const [saving, setSaving] = useState(false);

  const { control, handleSubmit, reset } = useForm<IndexerFormValues>({
    defaultValues: {
      name: "",
      type: "Torznab",
      baseUrl: "",
      authMode: "ApiKey",
      apiKey: "",
      username: "",
      password: "",
      enabled: true,
    },
  });

  const watchedValues = useWatch({ control });

  useEffect(() => {
    if (open) {
      if (editing) {
        reset({
          name: editing.name,
          type: editing.type as "Torznab" | "Rss",
          baseUrl: editing.baseUrl,
          authMode: editing.authMode as "None" | "ApiKey" | "BasicAuth",
          apiKey: "",
          username: "",
          password: "",
          enabled: editing.enabled,
        });
      } else {
        reset({
          name: "",
          type: "Torznab",
          baseUrl: "",
          authMode: "ApiKey",
          apiKey: "",
          username: "",
          password: "",
          enabled: true,
        });
      }
    }
  }, [open, editing, reset]);

  const onSubmit = async (values: IndexerFormValues) => {
    setSaving(true);
    try {
      const req: CreateIndexerRequest = {
        name: values.name,
        type: values.type,
        baseUrl: values.baseUrl,
        authMode: values.authMode,
        apiKey: values.authMode === "ApiKey" ? values.apiKey : undefined,
        username: values.authMode === "BasicAuth" ? values.username : undefined,
        password: values.authMode === "BasicAuth" ? values.password : undefined,
        enabled: values.enabled,
      };

      if (editing) {
        const updated = await updateIndexerApi(editing.id, req);
        onSaved(updated, false);
        toast.success("Indexer updated");
      } else {
        const created = await createIndexerApi(req);
        onSaved(created, true);
        toast.success("Indexer created");
      }
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to save indexer");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editing ? "Edit Indexer" : "Add Indexer"}</DialogTitle>
          <DialogDescription>
            {editing
              ? "Update the indexer configuration."
              : "Configure a new indexer to search for torrents."}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(onSubmit)} className={styles.formGrid}>
          <FormField label="Name" control={control} name="name" />
          <div className={styles.formRow}>
            <label className={styles.formLabel}>Type</label>
            <SelectField control={control} name="type" options={["Torznab", "Rss"]} />
          </div>
          <FormField label="Base URL" control={control} name="baseUrl" />
          <div className={styles.formRow}>
            <label className={styles.formLabel}>Auth</label>
            <SelectField
              control={control}
              name="authMode"
              options={["None", "ApiKey", "BasicAuth"]}
            />
          </div>
          {watchedValues.authMode === "ApiKey" && (
            <FormField label="API Key" control={control} name="apiKey" />
          )}
          {watchedValues.authMode === "BasicAuth" && (
            <>
              <FormField label="Username" control={control} name="username" />
              <FormField label="Password" control={control} name="password" type="password" />
            </>
          )}
          <div className={styles.formRow}>
            <label className={styles.formLabel}>Enabled</label>
            <Controller
              name="enabled"
              control={control}
              render={({ field }) => (
                <input
                  type="checkbox"
                  checked={field.value}
                  onChange={(e) => field.onChange(e.target.checked)}
                />
              )}
            />
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              Cancel
            </Button>
            <Button type="submit" loading={saving}>
              {editing ? "Save" : "Create"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// --- Tiny form helpers ---

function FormField<T extends FieldValues>({
  label,
  control,
  name,
  type = "text",
}: {
  label: string;
  control: Control<T>;
  name: Path<T>;
  type?: string;
}) {
  return (
    <div className={styles.formRow}>
      <label className={styles.formLabel}>{label}</label>
      <Controller
        name={name}
        control={control}
        render={({ field }) => <Input {...field} type={type} />}
      />
    </div>
  );
}

function SelectField<T extends FieldValues>({
  control,
  name,
  options,
}: {
  control: Control<T>;
  name: Path<T>;
  options: string[];
}) {
  return (
    <Controller
      name={name}
      control={control}
      render={({ field }) => (
        <select
          {...field}
          className="flex h-10 md:h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          {options.map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </select>
      )}
    />
  );
}

// --- Formatting helpers ---

function formatSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  const units = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const value = bytes / Math.pow(1024, i);
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleDateString();
  } catch {
    return iso;
  }
}
