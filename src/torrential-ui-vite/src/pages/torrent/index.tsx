"use client";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleArrowDown,
  faCircleArrowUp,
  faCircleNotch,
  faCirclePause,
  faExclamationTriangle,
  faPause,
  faPlay,
  faPlus,
  faTrash,
} from "@fortawesome/free-solid-svg-icons";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSelector } from "react-redux";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import {
  selectTorrentsByInfoHashes,
  torrentsWithPeersSelector,
  useAppDispatch,
} from "../../store";
import { TorrentsState, setTorrents } from "../../store/slices/torrentsSlice";
import {
  addTorrent,
  fetchTorrents as fetchTorrentsApi,
  PeerApiModel,
  TorrentApiModel,
  previewTorrent,
  TorrentPreviewApiModel,
} from "../../services/api";
import { PeerSummary, TorrentPreviewSummary, TorrentSummary } from "../../types";
import { TorrentFilePreviewModal } from "../../components/TorrentPreviewModal/torrent-preview-modal";
import { setPeers } from "../../store/slices/peersSlice";
import { selectTorrentForDetail } from "../../store/slices/torrentDetailSlice";
import { FileUpload, FileUploadElement } from "../../components/FileUpload/file-upload";
import Layout from "../layout";
import { useLayoutContext, statusFilterLabels } from "../layout";
import { AlfredContext, setContext } from "../../store/slices/alfredSlice";
import { DetailPane } from "./detail-pane";
import { ScrollArea } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";

export default function TorrentPage() {
  return (
    <Layout>
      <Page />
    </Layout>
  );
}

function Page() {
  const dispatch = useAppDispatch();
  const torrents = useSelector(torrentsWithPeersSelector);
  const [selectedTorrents, setSelectedTorrents] = useState<string[]>([]);
  const [currentPosition, setCurrentPosition] = useState(0);
  const { filterText, statusFilter, openedInfoHash, setOpenedInfoHash } = useLayoutContext();

  useEffect(() => {
    dispatch(setContext(AlfredContext.TorrentList));
  }, [dispatch]);

  const { enableScope } = useHotkeysContext();
  useEffect(() => {
    enableScope("torrents");
  }, [enableScope]);

  const selectTorrent = (infoHash: string) => {
    if (selectedTorrents.includes(infoHash)) {
      setSelectedTorrents(selectedTorrents.filter((item) => item !== infoHash));
    } else {
      setSelectedTorrents([...selectedTorrents, infoHash]);
    }
  };

  const isSelected = (infoHash: string) => {
    return selectedTorrents.includes(infoHash);
  };

  const fetchTorrents = useCallback(async () => {
    try {
      const data = await fetchTorrentsApi();

      const mappedTorrents = data.reduce((pv: TorrentsState, cv: TorrentApiModel) => {
        const summary: TorrentSummary = {
          name: cv.name,
          infoHash: cv.infoHash,
          progress: cv.progress,
          uploadRate: cv.uploadRate,
          downloadRate: cv.downloadRate,
          sizeInBytes: cv.totalSizeBytes,
          status: cv.status,
          bytesUploaded: cv.bytesUploaded,
          bytesDownloaded: cv.bytesDownloaded,
        };

        pv[cv.infoHash] = summary;
        return pv;
      }, {});
      dispatch(setTorrents(mappedTorrents));

      data.forEach((torrent: TorrentApiModel) => {
        const torrentPeers: PeerSummary[] = torrent.peers.reduce(
          (tpv: PeerSummary[], p: PeerApiModel) => {
            const summary: PeerSummary = {
              infoHash: torrent.infoHash,
              ip: p.ipAddress,
              port: p.port,
              peerId: p.peerId,
              isSeed: p.isSeed,
            };

            tpv.push(summary);
            return tpv;
          },
          []
        );

        dispatch(setPeers({ infoHash: torrent.infoHash, peers: torrentPeers }));
      });
    } catch {
      console.log("error fetching torrents");
    }
  }, [dispatch]);

  useEffect(() => {
    fetchTorrents();
  }, [fetchTorrents]);

  const filteredTorrents = useMemo(() => {
    let result = torrents;

    // Apply status filter
    if (statusFilter === "downloading") {
      result = result.filter((t) => t.status === "Running" && (t.progress ?? 0) < 1);
    } else if (statusFilter === "completed") {
      result = result.filter((t) => (t.progress ?? 0) >= 1);
    } else if (statusFilter === "paused") {
      result = result.filter((t) => t.status === "Stopped" || t.status === "Idle");
    } else if (statusFilter === "active") {
      result = result.filter((t) => t.status === "Running");
    }

    // Apply text filter
    const normalizedFilter = filterText.trim().toLowerCase();
    if (normalizedFilter) {
      result = result.filter((torrent) => torrent.name.toLowerCase().includes(normalizedFilter));
    }

    return result;
  }, [filterText, statusFilter, torrents]);

  useHotkeys(
    "up",
    () => {
      if (filteredTorrents.length === 0) return;
      let nextId = currentPosition - 1;
      if (nextId < 0) nextId = filteredTorrents.length - 1;
      setCurrentPosition(nextId);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    () => {
      if (filteredTorrents.length === 0) return;
      let nextId = currentPosition + 1;
      if (nextId >= filteredTorrents.length) nextId = 0;
      setCurrentPosition(nextId);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "space",
    () => {
      if (filteredTorrents.length === 0) return;
      selectTorrent(filteredTorrents[currentPosition].infoHash);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  const focusedInfoHash = filteredTorrents[currentPosition]?.infoHash ?? null;
  const isDetailPaneOpen = openedInfoHash !== null;

  useEffect(() => {
    if (openedInfoHash && !filteredTorrents.some((torrent) => torrent.infoHash === openedInfoHash)) {
      setOpenedInfoHash(null);
    }
  }, [openedInfoHash, filteredTorrents, setOpenedInfoHash]);

  useEffect(() => {
    if (!focusedInfoHash && openedInfoHash !== null) {
      setOpenedInfoHash(null);
    }
  }, [focusedInfoHash, openedInfoHash, setOpenedInfoHash]);

  useEffect(() => {
    if (focusedInfoHash && openedInfoHash) {
      setOpenedInfoHash(focusedInfoHash);
    }
  }, [focusedInfoHash, openedInfoHash, setOpenedInfoHash]);

  useEffect(() => {
    dispatch(selectTorrentForDetail(openedInfoHash));
  }, [dispatch, openedInfoHash]);

  useHotkeys(
    "enter",
    () => {
      const currentTorrent = filteredTorrents[currentPosition];
      if (!currentTorrent) return;
      setOpenedInfoHash(currentTorrent.infoHash);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "esc",
    () => {
      setOpenedInfoHash(null);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useEffect(() => {
    if (currentPosition >= filteredTorrents.length && filteredTorrents.length > 0) {
      setCurrentPosition(0);
    }
  }, [currentPosition, filteredTorrents.length]);

  // Get focused torrent status for bottom action bar
  const focusedTorrent = filteredTorrents[currentPosition] ?? null;

  const memoActionRow = useMemo(() => {
    return (
      <ActionsRow
        selectedTorrents={selectedTorrents}
        setSelectedTorrents={setSelectedTorrents}
      />
    );
  }, [selectedTorrents]);

  return (
    <TooltipProvider>
      <div className="flex h-full min-h-0 overflow-hidden">
        {/* Main torrent list area */}
        <div className="flex min-w-0 flex-1 flex-col">
          {/* Section header */}
          <div className="flex items-center justify-between px-5 py-3">
            <h2 className="text-sm font-semibold tracking-wide">{statusFilterLabels[statusFilter]}</h2>
          </div>

          <ScrollArea className="min-h-0 flex-1">
            {filteredTorrents.length === 0 && (
              <div className="p-8 text-center text-sm text-muted-foreground">
                No torrents match your filter.
              </div>
            )}

            {filteredTorrents.length > 0 && (
              <div className="space-y-2 px-4 pb-4">
                {filteredTorrents.map((torrent, index) => (
                  <TorrentCard
                    toggleSelect={selectTorrent}
                    toggleFocus={() => {
                      setCurrentPosition(index);
                      setOpenedInfoHash(torrent.infoHash);
                    }}
                    isFocused={currentPosition === index}
                    isSelected={isSelected(torrent.infoHash)}
                    uploadRate={torrent.uploadRate}
                    downloadRate={torrent.downloadRate}
                    status={torrent.status}
                    key={torrent.infoHash}
                    progress={torrent.progress ?? 0}
                    infoHash={torrent.infoHash ?? ""}
                    seeders={
                      torrent.peers?.reduce((pv, cv) => {
                        if (cv.isSeed) return pv + 1;
                        return pv;
                      }, 0) ?? 0
                    }
                    leechers={
                      torrent.peers?.reduce((pv, cv) => {
                        if (!cv.isSeed) return pv + 1;
                        return pv;
                      }, 0) ?? 0
                    }
                    totalBytes={torrent.sizeInBytes ?? 0}
                    title={torrent.name ?? "???"}
                  />
                ))}
              </div>
            )}
          </ScrollArea>

          {/* Bottom action bar */}
          <BottomActionBar
            focusedTorrent={focusedTorrent}
            actionsRow={memoActionRow}
          />
        </div>

        {/* Right detail pane */}
        {isDetailPaneOpen && openedInfoHash && (
          <div className="hidden w-[380px] shrink-0 border-l border-border/50 overflow-hidden lg:block">
            <DetailPane infoHash={openedInfoHash} onClose={() => setOpenedInfoHash(null)} />
          </div>
        )}
      </div>
    </TooltipProvider>
  );
}

interface BottomActionBarProps {
  focusedTorrent: { status: string; progress: number; infoHash: string } | null;
  actionsRow: React.ReactNode;
}

function BottomActionBar({ focusedTorrent, actionsRow }: BottomActionBarProps) {
  const isPaused = focusedTorrent && (focusedTorrent.status === "Stopped" || focusedTorrent.status === "Idle");
  const isRunning = focusedTorrent && focusedTorrent.status === "Running";

  const startTorrent = async (infoHash: string) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/start`, { method: "POST" });
    } catch (e) {
      console.log(e);
    }
  };

  const stopTorrent = async (infoHash: string) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/stop`, { method: "POST" });
    } catch (e) {
      console.log(e);
    }
  };

  return (
    <div className="relative flex h-14 shrink-0 items-center justify-between border-t border-border/50 bg-card/50 px-5">
      {/* FAB - Add torrent button */}
      <button
        type="button"
        onClick={() => window.dispatchEvent(new CustomEvent("trigger-add-torrent"))}
        className="flex h-10 w-10 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg shadow-primary/25 transition-all hover:bg-primary/90 hover:shadow-xl hover:shadow-primary/30 active:scale-95"
        aria-label="Add Torrent"
      >
        <FontAwesomeIcon icon={faPlus} className="h-4 w-4" />
      </button>

      {/* Hidden actions row for modals/file upload */}
      <div className="hidden">{actionsRow}</div>

      {/* Context action button */}
      <div className="flex items-center gap-2">
        {focusedTorrent && isPaused && (
          <button
            type="button"
            onClick={() => startTorrent(focusedTorrent.infoHash)}
            className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2 text-sm font-medium text-primary-foreground transition-all hover:bg-primary/90 active:scale-[0.98]"
          >
            <FontAwesomeIcon icon={faPlay} className="h-3 w-3" />
            Resume Torrent
          </button>
        )}
        {focusedTorrent && isRunning && (
          <button
            type="button"
            onClick={() => stopTorrent(focusedTorrent.infoHash)}
            className="flex items-center gap-2 rounded-lg bg-amber-500/20 px-5 py-2 text-sm font-medium text-amber-400 transition-all hover:bg-amber-500/30 active:scale-[0.98]"
          >
            <FontAwesomeIcon icon={faPause} className="h-3 w-3" />
            Pause Torrent
          </button>
        )}
      </div>
    </div>
  );
}

interface ActionsRowProps {
  selectedTorrents: string[];
  setSelectedTorrents: (infoHashes: string[]) => void;
}

const ActionsRow = ({
  selectedTorrents,
  setSelectedTorrents,
}: ActionsRowProps) => {
  const uploadRef = useRef<FileUploadElement | null>(null);
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<TorrentPreviewSummary | null>(null);
  const [selectedFileIds, setSelectedFileIds] = useState<number[]>([]);
  const [previewModalOpen, setPreviewModalOpen] = useState(false);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [isAddLoading, setIsAddLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [addError, setAddError] = useState<string | null>(null);
  const [completedPathOverride, setCompletedPathOverride] = useState("");
  const [desiredSeedTimeDays, setDesiredSeedTimeDays] = useState("");
  const [defaultSeedTimeDays, setDefaultSeedTimeDays] = useState("");

  useEffect(() => {
    fetch(`${import.meta.env.VITE_API_BASE_URL}/settings/seed`)
      .then(res => res.json())
      .then(json => {
        const days = `${json.data.desiredSeedTimeDays}`;
        setDefaultSeedTimeDays(days);
      })
      .catch(() => {});
  }, []);

  // Listen for trigger-add-torrent event from header and FAB
  useEffect(() => {
    const handler = () => {
      if (uploadRef.current) uploadRef.current.openFilePicker();
    };
    window.addEventListener("trigger-add-torrent", handler);
    return () => window.removeEventListener("trigger-add-torrent", handler);
  }, []);

  const resetPreviewState = () => {
    setSelectedFile(null);
    setPreview(null);
    setSelectedFileIds([]);
    setPreviewModalOpen(false);
    setIsAddLoading(false);
    setAddError(null);
    setCompletedPathOverride("");
    setDesiredSeedTimeDays(defaultSeedTimeDays);
  };

  const mapPreview = (model: TorrentPreviewApiModel): TorrentPreviewSummary => {
    return {
      name: model.name,
      infoHash: model.infoHash,
      totalSizeBytes: model.totalSizeBytes,
      files: model.files.map((f) => ({
        id: f.id,
        filename: f.filename,
        sizeBytes: f.sizeBytes,
      })),
    };
  };

  const onUpload = async (file: File) => {
    setPreviewError(null);
    setAddError(null);
    setIsPreviewLoading(true);
    setSelectedFile(file);

    try {
      const previewResult = await previewTorrent(file);
      const previewSummary = mapPreview(previewResult);

      setPreview(previewSummary);
      setSelectedFileIds(previewSummary.files.map((f) => f.id));
      setPreviewModalOpen(true);
      setDesiredSeedTimeDays(defaultSeedTimeDays);
    } catch (error) {
      console.error("Error previewing torrent file:", error);
      setPreviewError("Failed to preview torrent file.");
      resetPreviewState();
    } finally {
      setIsPreviewLoading(false);
    }
  };

  const removeTorrent = async (infoHash: string, deleteFiles: boolean) => {
    try {
      const body = { deleteFiles };
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/delete`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
    } catch (e) {
      console.log(e);
    }
  };

  const removeTorrents = async (deleteFiles: boolean) => {
    selectedTorrents.forEach((infoHash) => {
      removeTorrent(infoHash, deleteFiles);
    });
    setSelectedTorrents([]);
  };

  const toggleFileSelection = (id: number) => {
    setSelectedFileIds((current) => {
      if (current.includes(id)) {
        return current.filter((x) => x !== id);
      }
      return [...current, id];
    });
  };

  const toggleAllFileSelection = () => {
    if (!preview) return;
    setSelectedFileIds((current) => {
      const allIds = preview.files.map((f) => f.id);
      if (current.length === allIds.length) return [];
      return allIds;
    });
  };

  const confirmAddTorrent = async () => {
    if (selectedFile === null) {
      setAddError("No torrent file selected.");
      return;
    }

    setAddError(null);
    setIsAddLoading(true);

    try {
      const seedTime = desiredSeedTimeDays.trim() ? parseInt(desiredSeedTimeDays, 10) : undefined;
      await addTorrent(selectedFile, selectedFileIds, completedPathOverride.trim() || undefined, seedTime);
      resetPreviewState();
    } catch (e) {
      console.error("Error adding torrent:", e);
      setAddError("Failed to add torrent.");
    } finally {
      setIsAddLoading(false);
    }
  };

  const closePreviewModal = () => {
    resetPreviewState();
  };

  return (
    <div className="flex items-center gap-2">
      <FileUpload accept=".torrent" ref={uploadRef} onFileChange={(f) => onUpload(f)} />

      <TorrentRemoveConfirmationModal
        open={deleteModalOpen}
        infoHashes={selectedTorrents}
        onClose={() => setDeleteModalOpen(false)}
        onRemove={(_, deleteFiles) => removeTorrents(deleteFiles)}
      />

      <TorrentFilePreviewModal
        open={previewModalOpen}
        preview={preview}
        selectedFileIds={selectedFileIds}
        isAddLoading={isAddLoading}
        addError={addError}
        completedPathOverride={completedPathOverride}
        onCompletedPathChange={setCompletedPathOverride}
        desiredSeedTimeDays={desiredSeedTimeDays}
        onDesiredSeedTimeDaysChange={setDesiredSeedTimeDays}
        onClose={closePreviewModal}
        onConfirm={confirmAddTorrent}
        onToggleFile={toggleFileSelection}
        onToggleAllFiles={toggleAllFileSelection}
      />

      {previewError && <p className="text-xs text-destructive">{previewError}</p>}
    </div>
  );
};

interface TorrentCardProps {
  infoHash: string;
  title: string;
  progress: number;
  totalBytes: number;
  seeders: number;
  leechers: number;
  status: string;
  uploadRate: number;
  downloadRate: number;
  isFocused: boolean;
  isSelected: boolean;
  toggleSelect: (infoHash: string) => void;
  toggleFocus: () => void;
}

function TorrentCard({
  infoHash,
  title,
  progress,
  totalBytes,
  seeders,
  leechers,
  status,
  uploadRate,
  downloadRate,
  isFocused,
  isSelected,
  toggleSelect,
  toggleFocus,
}: TorrentCardProps) {
  function prettyPrintBytes(bytes: number) {
    const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
    if (bytes === 0) return "0 Byte";
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    if (i === 0) return bytes + " " + sizes[i];
    return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
  }

  const statusIcon =
    status === "Running" && progress < 1 ? faCircleArrowDown :
    status === "Running" && progress >= 1 ? faCircleArrowUp :
    status === "Stopped" || status === "Idle" ? faCirclePause :
    status === "Failed" ? faExclamationTriangle :
    faCircleNotch;

  const statusLabel =
    status === "Running" && progress < 1 ? "DOWNLOADING" :
    status === "Running" && progress >= 1 ? "SEEDING" :
    status === "Stopped" || status === "Idle" ? "PAUSED" :
    status === "Failed" ? "FAILED" :
    status.toUpperCase();

  const statusBadgeClass =
    status === "Running" && progress < 1 ? "bg-primary/15 text-primary border-primary/25" :
    status === "Running" && progress >= 1 ? "bg-emerald-500/15 text-emerald-400 border-emerald-500/25" :
    status === "Stopped" || status === "Idle" ? "bg-amber-500/15 text-amber-400 border-amber-500/25" :
    status === "Failed" ? "bg-red-500/15 text-red-400 border-red-500/25" :
    "bg-muted text-muted-foreground";

  const statusDotColor =
    status === "Running" && progress < 1 ? "bg-primary" :
    status === "Running" && progress >= 1 ? "bg-emerald-400" :
    status === "Stopped" || status === "Idle" ? "bg-amber-400" :
    status === "Failed" ? "bg-red-400" :
    "bg-muted-foreground";

  // Estimate time remaining
  const bytesRemaining = totalBytes * (1 - progress);
  const etaText = downloadRate > 0 && progress < 1
    ? formatEta(bytesRemaining / downloadRate)
    : null;

  return (
    <div
      className={cn(
        "group relative cursor-pointer rounded-lg border border-border/50 bg-card/50 px-4 py-3 transition-all hover:bg-card/80 hover:border-border/70",
        isFocused && "border-l-2 border-l-primary bg-card/70 border-border/60",
        isSelected && "bg-primary/5 border-primary/20"
      )}
      tabIndex={0}
      role="button"
      aria-selected={isSelected}
      onClick={() => toggleFocus()}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          toggleFocus();
        }
      }}
    >
      <div className="flex items-start gap-3">
        {/* Status dot */}
        <div className="flex flex-col items-center gap-1 pt-1">
          <div
            className={cn(
              "opacity-0 transition-opacity group-hover:opacity-100",
              isSelected && "opacity-100"
            )}
            onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => event.stopPropagation()}
          >
            <Checkbox
              checked={isSelected}
              onCheckedChange={() => toggleSelect(infoHash)}
              aria-label={`Select ${title}`}
              className="h-3.5 w-3.5"
            />
          </div>
          <div className={cn("h-2 w-2 rounded-full", statusDotColor, isSelected && "hidden")} />
        </div>

        {/* Center content */}
        <div className="min-w-0 flex-1 space-y-1.5">
          <div className="flex items-center justify-between gap-3">
            <p className="truncate text-sm font-semibold">{title}</p>
            <div className="shrink-0 text-right">
              <p className="text-sm font-medium text-primary">
                {prettyPrintBytes(downloadRate)}/s
              </p>
              {etaText && (
                <p className="text-[10px] uppercase tracking-wider text-muted-foreground">
                  {etaText} left
                </p>
              )}
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className={cn("inline-flex items-center gap-1 rounded border px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wider", statusBadgeClass)}>
              <FontAwesomeIcon icon={statusIcon} className="h-2.5 w-2.5" />
              {statusLabel}
            </span>
            <span className="text-xs text-muted-foreground">
              {prettyPrintBytes(totalBytes * progress)} / {prettyPrintBytes(totalBytes)}
            </span>
          </div>

          <Progress value={progress * 100} className="h-1" />
        </div>
      </div>
    </div>
  );
}

function formatEta(seconds: number): string {
  if (!isFinite(seconds) || seconds <= 0) return "";
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m} min`;
  return `${Math.floor(seconds)}s`;
}

interface TorrentRemoveConfirmationModalProps {
  open: boolean;
  infoHashes: string[];
  onClose: () => void;
  onRemove: (infoHashes: string[], deleteFiles: boolean) => void;
}

function TorrentRemoveConfirmationModal({
  infoHashes,
  onClose,
  onRemove,
  open,
}: TorrentRemoveConfirmationModalProps) {
  const title = useMemo(() => {
    if (!infoHashes || infoHashes.length <= 1) {
      return "Remove Torrent";
    }

    return "Remove Torrents";
  }, [infoHashes]);

  const [deleteFiles, setDeleteFiles] = useState(false);
  const torrents = useSelector(selectTorrentsByInfoHashes(infoHashes));

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !nextOpen && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>Are you sure you want remove:</DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <ul className="max-h-64 list-disc space-y-1 overflow-auto pl-6 text-sm">
            {infoHashes.map((hash) => {
              return (
                <li key={hash}>
                  <p>{torrents[hash].name}</p>
                </li>
              );
            })}
          </ul>
          <label className="inline-flex items-center gap-2 text-sm">
            <Checkbox checked={deleteFiles} onCheckedChange={(checked) => setDeleteFiles(checked === true)} />
            <span>Delete files on disk</span>
          </label>
        </div>
        <DialogFooter>
          <Button
            variant="destructive"
            onClick={() => {
              onRemove(infoHashes, deleteFiles);
              onClose();
            }}
            type="button"
          >
            Remove
          </Button>
          <Button variant="secondary" onClick={onClose} type="button">
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
