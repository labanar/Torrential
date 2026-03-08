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
  previewTorrentFromUrl,
  addTorrentFromUrl,
  type IndexerVm,
  type CreateIndexerRequest,
  type SearchResultVm,
  type TorrentPreviewApiModel,
} from "../../services/api";
import { TorrentPreviewSummary } from "../../types";
import { TorrentFilePreviewModal } from "../../components/TorrentPreviewModal/torrent-preview-modal";
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
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Control, Controller, FieldValues, Path } from "react-hook-form";

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

  const handleTest = useCallback(async (id: string) => {
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
  }, []);

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
    <div className="mx-auto flex h-full w-full max-w-6xl min-h-0 flex-col gap-4 overflow-auto p-4 md:p-6">
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <CardTitle className="text-2xl">Integrations</CardTitle>
            <Button onClick={openCreate} size="sm" aria-label="Add indexer">
              <FontAwesomeIcon icon={faPlus} />
              <span>Add indexer</span>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <h2 className="text-lg font-semibold">Indexers</h2>
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

          <h2 className="text-lg font-semibold">Search</h2>
          <SearchSection
            loading={searchLoading}
            results={searchResults}
            query={searchQuery}
            hasEnabledIndexers={indexers.some((i) => i.enabled)}
            dispatch={dispatch}
          />
        </CardContent>
      </Card>

      <IndexerFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editing={editingIndexer}
        onSaved={handleSaved}
      />
    </div>
  );
}

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
    return <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">Loading indexers...</div>;
  }

  if (indexers.length === 0) {
    return <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">No indexers configured</div>;
  }

  return (
    <div className="grid gap-2">
      {indexers.map((indexer) => (
        <div
          key={indexer.id}
          className="grid gap-3 rounded-md border p-3 md:grid-cols-[1fr_auto] md:items-center"
        >
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="truncate text-sm font-semibold">{indexer.name}</p>
              <Badge variant={indexer.enabled ? "secondary" : "outline"}>
                {indexer.enabled ? "Enabled" : "Disabled"}
              </Badge>
            </div>
            <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
              <span>{indexer.type}</span>
              <span className="truncate">{indexer.baseUrl}</span>
            </div>
          </div>
          <div className="flex items-center gap-1">
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

interface SearchSectionProps {
  loading: boolean;
  results: SearchResultVm[];
  query: string;
  hasEnabledIndexers: boolean;
  dispatch: ReturnType<typeof useAppDispatch>;
}

function SearchSection({ loading, results, query, hasEnabledIndexers, dispatch }: SearchSectionProps) {
  const [searchInput, setSearchInput] = useState("");

  const [pendingDownloadUrl, setPendingDownloadUrl] = useState<string | null>(null);
  const [pendingIndexerId, setPendingIndexerId] = useState<string | null>(null);
  const [preview, setPreview] = useState<TorrentPreviewSummary | null>(null);
  const [selectedFileIds, setSelectedFileIds] = useState<number[]>([]);
  const [previewModalOpen, setPreviewModalOpen] = useState(false);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [isAddLoading, setIsAddLoading] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [completedPathOverride, setCompletedPathOverride] = useState("");

  const resetPreviewState = () => {
    setPendingDownloadUrl(null);
    setPendingIndexerId(null);
    setPreview(null);
    setSelectedFileIds([]);
    setPreviewModalOpen(false);
    setIsAddLoading(false);
    setAddError(null);
    setCompletedPathOverride("");
  };

  const mapPreview = (model: TorrentPreviewApiModel): TorrentPreviewSummary => ({
    name: model.name,
    infoHash: model.infoHash,
    totalSizeBytes: model.totalSizeBytes,
    files: model.files.map((f) => ({
      id: f.id,
      filename: f.filename,
      sizeBytes: f.sizeBytes,
    })),
  });

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

    setIsPreviewLoading(true);
    setAddError(null);
    setPendingDownloadUrl(result.downloadUrl);
    setPendingIndexerId(result.indexerId);

    try {
      const previewResult = await previewTorrentFromUrl(result.downloadUrl, result.indexerId);
      const previewSummary = mapPreview(previewResult);
      setPreview(previewSummary);
      setSelectedFileIds(previewSummary.files.map((f) => f.id));
      setPreviewModalOpen(true);
    } catch {
      toast.error("Failed to preview torrent");
      resetPreviewState();
    } finally {
      setIsPreviewLoading(false);
    }
  }, []);

  const confirmAddTorrent = async () => {
    if (!pendingDownloadUrl) {
      setAddError("No download URL.");
      return;
    }

    setAddError(null);
    setIsAddLoading(true);

    try {
      await addTorrentFromUrl(
        pendingDownloadUrl,
        selectedFileIds,
        completedPathOverride.trim() || undefined,
        pendingIndexerId
      );
      resetPreviewState();
    } catch {
      setAddError("Failed to add torrent.");
    } finally {
      setIsAddLoading(false);
    }
  };

  const toggleFileSelection = (id: number) => {
    setSelectedFileIds((current) =>
      current.includes(id) ? current.filter((x) => x !== id) : [...current, id]
    );
  };

  const toggleAllFileSelection = () => {
    if (!preview) return;
    setSelectedFileIds((current) => {
      const allIds = preview.files.map((f) => f.id);
      return current.length === allIds.length ? [] : allIds;
    });
  };

  return (
    <div className="space-y-3">
      <TorrentFilePreviewModal
        open={previewModalOpen}
        preview={preview}
        selectedFileIds={selectedFileIds}
        isAddLoading={isAddLoading}
        addError={addError}
        completedPathOverride={completedPathOverride}
        onCompletedPathChange={setCompletedPathOverride}
        onClose={resetPreviewState}
        onConfirm={confirmAddTorrent}
        onToggleFile={toggleFileSelection}
        onToggleAllFiles={toggleAllFileSelection}
      />

      <div className="flex flex-col gap-2 sm:flex-row">
        <Input
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
        <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          No results for "{query}"
        </div>
      )}

      {results.length > 0 && (
        <ScrollArea className="max-h-[28rem] rounded-md border">
          <div className="space-y-2 p-2">
            {results.map((r, i) => (
              <div key={`${r.title}-${i}`} className="rounded-md border p-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-semibold" title={r.title}>
                      {r.title}
                    </p>
                    <div className="mt-1 flex flex-wrap gap-2 text-xs text-muted-foreground">
                      <span>{formatSize(r.sizeBytes)}</span>
                      <span className="text-emerald-500">S: {r.seeders}</span>
                      <span className="text-red-500">L: {r.leechers}</span>
                      <span>{r.indexerName ?? "-"}</span>
                      <span>{r.publishDate ? formatDate(r.publishDate) : "-"}</span>
                    </div>
                  </div>
                  {r.downloadUrl && (
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={`Add ${r.title}`}
                      onClick={() => handleAddTorrent(r)}
                      loading={isPreviewLoading && pendingDownloadUrl === r.downloadUrl}
                    >
                      <FontAwesomeIcon icon={faDownload} />
                      Add
                    </Button>
                  )}
                </div>

                {r.metadata && (
                  <div className="mt-3 flex gap-3 border-t pt-3">
                    {r.metadata.artworkUrl && (
                      <img src={r.metadata.artworkUrl} alt="" className="h-20 w-14 rounded object-cover" />
                    )}
                    <div className="space-y-1 text-xs text-muted-foreground">
                      {r.metadata.year && <p>Year: {r.metadata.year}</p>}
                      {r.metadata.genre && <p>Genre: {r.metadata.genre}</p>}
                      {r.metadata.description && <p>{r.metadata.description}</p>}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </ScrollArea>
      )}
    </div>
  );
}

interface IndexerFormValues {
  name: string;
  type: "Torznab" | "Rss" | "TorrentLeech";
  baseUrl: string;
  authMode: "None" | "ApiKey" | "BasicAuth" | "Cookie";
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

  const { control, handleSubmit, reset, setValue } = useForm<IndexerFormValues>({
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
          type: editing.type as IndexerFormValues["type"],
          baseUrl: editing.baseUrl,
          authMode: editing.authMode as IndexerFormValues["authMode"],
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

  useEffect(() => {
    if (watchedValues.type === "TorrentLeech" && watchedValues.authMode !== "Cookie") {
      setValue("authMode", "Cookie");
    }
  }, [watchedValues.type, watchedValues.authMode, setValue]);

  const onSubmit = async (values: IndexerFormValues) => {
    setSaving(true);
    try {
      const needsCredentials = values.authMode === "BasicAuth" || values.authMode === "Cookie";
      const req: CreateIndexerRequest = {
        name: values.name,
        type: values.type,
        baseUrl: values.baseUrl,
        authMode: values.authMode,
        apiKey: values.authMode === "ApiKey" ? values.apiKey : undefined,
        username: needsCredentials ? values.username : undefined,
        password: needsCredentials ? values.password : undefined,
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
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
          <FormField label="Name" control={control} name="name" />
          <div className="grid gap-2">
            <Label>Type</Label>
            <SelectField control={control} name="type" options={["Torznab", "Rss", "TorrentLeech"]} />
          </div>
          <FormField label="Base URL" control={control} name="baseUrl" />
          {watchedValues.type !== "TorrentLeech" && (
            <div className="grid gap-2">
              <Label>Auth</Label>
              <SelectField control={control} name="authMode" options={["None", "ApiKey", "BasicAuth"]} />
            </div>
          )}
          {watchedValues.authMode === "ApiKey" && (
            <FormField label="API Key" control={control} name="apiKey" />
          )}
          {(watchedValues.authMode === "BasicAuth" || watchedValues.authMode === "Cookie") && (
            <>
              <FormField label="Username" control={control} name="username" />
              <FormField label="Password" control={control} name="password" type="password" />
            </>
          )}
          <div className="flex items-center gap-2">
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
            <Label>Enabled</Label>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
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
    <div className="grid gap-2">
      <Label>{label}</Label>
      <Controller name={name} control={control} render={({ field }) => <Input {...field} type={type} />} />
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
          className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
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
