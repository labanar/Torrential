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
import { Input } from "@/components/ui/input";
import { Progress } from "@/components/ui/progress";
import { Separator } from "@/components/ui/separator";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleArrowDown,
  faCircleArrowUp,
  faCircleNotch,
  faCirclePause,
  faDownLong,
  faGripLines,
  faPause,
  faPlay,
  faPlus,
  faSeedling,
  faTrash,
  faUpLong,
  faUserGroup,
} from "@fortawesome/free-solid-svg-icons";
import { type PointerEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  previewTorrent,
  TorrentApiModel,
  TorrentPreviewApiModel,
} from "../../services/api";
import { PeerSummary, TorrentPreviewSummary, TorrentSummary } from "../../types";
import { TorrentFilePreviewModal } from "../../components/TorrentPreviewModal/torrent-preview-modal";
import { setPeers } from "../../store/slices/peersSlice";
import { selectTorrentForDetail } from "../../store/slices/torrentDetailSlice";
import { FileUpload, FileUploadElement } from "../../components/FileUpload/file-upload";
import Layout from "../layout";
import { AlfredContext, setContext } from "../../store/slices/alfredSlice";
import { DetailPane } from "./detail-pane";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Card } from "@/components/ui/card";
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
  const [openedInfoHash, setOpenedInfoHash] = useState<string | null>(null);
  const [detailPaneHeight, setDetailPaneHeight] = useState(320);
  const [isResizingDetailPane, setIsResizingDetailPane] = useState(false);
  const [filterText, setFilterText] = useState("");
  const splitRootRef = useRef<HTMLDivElement | null>(null);
  const resizeStartRef = useRef<{ startY: number; startHeight: number } | null>(null);

  const memoActionRow = useMemo(() => {
    return (
      <ActionsRow
        selectedTorrents={selectedTorrents}
        setSelectedTorrents={setSelectedTorrents}
        filterText={filterText}
        onFilterChange={setFilterText}
      />
    );
  }, [filterText, selectedTorrents]);

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
    const normalizedFilter = filterText.trim().toLowerCase();
    if (!normalizedFilter) {
      return torrents;
    }

    return torrents.filter((torrent) => torrent.name.toLowerCase().includes(normalizedFilter));
  }, [filterText, torrents]);

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

  const getPaneHeightBounds = useCallback(() => {
    const rootHeight = splitRootRef.current?.getBoundingClientRect().height ?? window.innerHeight;
    const isMobile = window.matchMedia("(max-width: 768px)").matches;
    const minHeight = isMobile ? 180 : 220;
    const maxHeight = Math.max(minHeight + 40, Math.floor(rootHeight * (isMobile ? 0.7 : 0.65)));
    return { minHeight, maxHeight };
  }, []);

  const clampDetailPaneHeight = useCallback(
    (height: number) => {
      const { minHeight, maxHeight } = getPaneHeightBounds();
      return Math.max(minHeight, Math.min(maxHeight, height));
    },
    [getPaneHeightBounds]
  );

  const adjustDetailPaneHeight = useCallback(
    (delta: number) => {
      setDetailPaneHeight((current) => clampDetailPaneHeight(current + delta));
    },
    [clampDetailPaneHeight]
  );

  useEffect(() => {
    const handleResize = () => {
      setDetailPaneHeight((current) => clampDetailPaneHeight(current));
    };

    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
    };
  }, [clampDetailPaneHeight]);

  useEffect(() => {
    if (isDetailPaneOpen) {
      setDetailPaneHeight((current) => clampDetailPaneHeight(current));
    }
  }, [clampDetailPaneHeight, isDetailPaneOpen]);

  useEffect(() => {
    if (openedInfoHash && !filteredTorrents.some((torrent) => torrent.infoHash === openedInfoHash)) {
      setOpenedInfoHash(null);
    }
  }, [openedInfoHash, filteredTorrents]);

  useEffect(() => {
    if (!focusedInfoHash && openedInfoHash !== null) {
      setOpenedInfoHash(null);
    }
  }, [focusedInfoHash, openedInfoHash]);

  useEffect(() => {
    if (focusedInfoHash && openedInfoHash) {
      setOpenedInfoHash(focusedInfoHash);
    }
  }, [focusedInfoHash, openedInfoHash]);

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

  const onSplitterPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (!isDetailPaneOpen) return;
    resizeStartRef.current = { startY: event.clientY, startHeight: detailPaneHeight };
    setIsResizingDetailPane(true);
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onSplitterPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    if (!resizeStartRef.current) return;
    const delta = resizeStartRef.current.startY - event.clientY;
    const targetHeight = resizeStartRef.current.startHeight + delta;
    setDetailPaneHeight(clampDetailPaneHeight(targetHeight));
  };

  const stopSplitterResize = (event: PointerEvent<HTMLDivElement>) => {
    if (resizeStartRef.current) {
      resizeStartRef.current = null;
      setIsResizingDetailPane(false);
    }
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
  };

  return (
    <TooltipProvider>
      <div className="flex h-full min-h-0 flex-col overflow-hidden" ref={splitRootRef}>
        <div className="flex min-h-0 flex-1 flex-col">
          {memoActionRow}
          <Separator />
          <ScrollArea className="min-h-0 flex-1">
            <div className="space-y-2 p-3">
              {filteredTorrents.length === 0 && (
                <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
                  No torrents match your filter.
                </div>
              )}

              {filteredTorrents.map((torrent, index) => (
                <TorrentRow
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
          </ScrollArea>
        </div>

        {isDetailPaneOpen && openedInfoHash && (
          <>
            <div
              className={cn(
                "flex h-4 cursor-ns-resize items-center justify-center border-y text-muted-foreground",
                isResizingDetailPane && "bg-muted text-foreground"
              )}
              role="separator"
              aria-label="Resize torrent details pane"
              aria-orientation="horizontal"
              tabIndex={0}
              onPointerDown={onSplitterPointerDown}
              onPointerMove={onSplitterPointerMove}
              onPointerUp={stopSplitterResize}
              onPointerCancel={stopSplitterResize}
              onKeyDown={(event) => {
                if (event.key === "ArrowUp") {
                  event.preventDefault();
                  adjustDetailPaneHeight(24);
                } else if (event.key === "ArrowDown") {
                  event.preventDefault();
                  adjustDetailPaneHeight(-24);
                } else if (event.key === "Home") {
                  event.preventDefault();
                  const { minHeight } = getPaneHeightBounds();
                  setDetailPaneHeight(minHeight);
                } else if (event.key === "End") {
                  event.preventDefault();
                  const { maxHeight } = getPaneHeightBounds();
                  setDetailPaneHeight(maxHeight);
                }
              }}
            >
              <FontAwesomeIcon icon={faGripLines} />
            </div>
            <div className="overflow-hidden" style={{ height: `${detailPaneHeight}px` }}>
              <DetailPane infoHash={openedInfoHash} onClose={() => setOpenedInfoHash(null)} />
            </div>
          </>
        )}
      </div>
    </TooltipProvider>
  );
}

interface ActionsRowProps {
  selectedTorrents: string[];
  setSelectedTorrents: (infoHashes: string[]) => void;
  filterText: string;
  onFilterChange: (value: string) => void;
}

const ActionsRow = ({
  selectedTorrents,
  setSelectedTorrents,
  filterText,
  onFilterChange,
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

  const resetPreviewState = () => {
    setSelectedFile(null);
    setPreview(null);
    setSelectedFileIds([]);
    setPreviewModalOpen(false);
    setIsAddLoading(false);
    setAddError(null);
    setCompletedPathOverride("");
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
    } catch (error) {
      console.error("Error previewing torrent file:", error);
      setPreviewError("Failed to preview torrent file.");
      resetPreviewState();
    } finally {
      setIsPreviewLoading(false);
    }
  };

  const stopTorrent = async (infoHash: string) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/stop`, {
        method: "POST",
      });
    } catch (e) {
      console.log(e);
      console.log("Error stopping torrent");
    }
  };

  const startTorrent = async (infoHash: string) => {
    try {
      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/start`, {
        method: "POST",
      });
    } catch (e) {
      console.log(e);
      console.log("Error stopping torrent");
    }
  };

  const removeTorrent = async (infoHash: string, deleteFiles: boolean) => {
    try {
      const body = {
        deleteFiles,
      };

      await fetch(`${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/delete`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(body),
      });
    } catch (e) {
      console.log(e);
      console.log("Error deleting torrent");
    }
  };

  const stopTorrents = async () => {
    selectedTorrents.forEach((infoHash) => {
      stopTorrent(infoHash);
    });
  };

  const startTorrents = async () => {
    selectedTorrents.forEach((infoHash) => {
      startTorrent(infoHash);
    });
  };

  const removeTorrents = async (deleteFiles: boolean) => {
    selectedTorrents.forEach((infoHash) => {
      removeTorrent(infoHash, deleteFiles);
    });
    setSelectedTorrents([]);
  };

  const torrentActionsDisabled = useMemo(() => {
    return selectedTorrents.length === 0;
  }, [selectedTorrents]);

  const toggleFileSelection = (id: number) => {
    setSelectedFileIds((current) => {
      if (current.includes(id)) {
        return current.filter((x) => x !== id);
      }

      return [...current, id];
    });
  };

  const toggleAllFileSelection = () => {
    if (!preview) {
      return;
    }

    setSelectedFileIds((current) => {
      const allIds = preview.files.map((f) => f.id);
      if (current.length === allIds.length) {
        return [];
      }

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
      await addTorrent(selectedFile, selectedFileIds, completedPathOverride.trim() || undefined);
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
    <div className="flex flex-wrap items-center gap-3 p-3">
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
        onClose={closePreviewModal}
        onConfirm={confirmAddTorrent}
        onToggleFile={toggleFileSelection}
        onToggleAllFiles={toggleAllFileSelection}
      />

      <div className="min-w-52 flex-1">
        <Input placeholder="Filter torrents" value={filterText} onChange={(e) => onFilterChange(e.target.value)} />
        {previewError && <p className="mt-1 text-xs text-destructive">{previewError}</p>}
      </div>

      <div className="flex items-center gap-2">
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon"
              variant="outline"
              disabled={torrentActionsDisabled}
              className="border-emerald-600 text-emerald-600 hover:bg-emerald-600/10"
              aria-label="Start"
              type="button"
              onClick={() => startTorrents()}
            >
              <FontAwesomeIcon icon={faPlay} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Start</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon"
              variant="outline"
              type="button"
              disabled={torrentActionsDisabled}
              className="border-amber-500 text-amber-500 hover:bg-amber-500/10"
              aria-label="Stop"
              onClick={() => stopTorrents()}
            >
              <FontAwesomeIcon icon={faPause} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Stop</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon"
              variant="destructive"
              type="button"
              onClick={() => setDeleteModalOpen(true)}
              disabled={torrentActionsDisabled}
              aria-label="Remove"
            >
              <FontAwesomeIcon icon={faTrash} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Delete</TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon"
              type="button"
              aria-label="Add"
              loading={isPreviewLoading}
              onClick={() => {
                if (uploadRef === null || uploadRef.current === null) return;
                uploadRef.current.openFilePicker();
              }}
            >
              <FontAwesomeIcon icon={faPlus} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Add Torrent</TooltipContent>
        </Tooltip>
      </div>
    </div>
  );
};

interface TorrentRowProps {
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

function TorrentRow({
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
}: TorrentRowProps) {
  const statusColorClass = useMemo(() => {
    if (status === "Stopped" || status === "Idle") return "text-amber-500";
    if (status === "Running") return "text-emerald-500";
    if (status === "Verifying" || status === "Copying") return "text-sky-500";
    return "text-muted-foreground";
  }, [status]);

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
    faCircleNotch;

  return (
    <Card
      className={cn(
        "cursor-pointer border-border/70 p-3 transition-colors",
        isFocused && "ring-1 ring-ring",
        isSelected && "bg-accent/50"
      )}
      onClick={() => toggleFocus()}
    >
      <div className="flex items-start gap-3">
        <div className="pt-1">
          <Checkbox checked={isSelected} onCheckedChange={() => toggleSelect(infoHash)} />
        </div>

        <div className="min-w-0 flex-1 space-y-2">
          <div className="flex items-center gap-2">
            <span className={cn("inline-flex h-6 w-6 items-center justify-center", statusColorClass)}>
              <FontAwesomeIcon icon={statusIcon} spin={statusIcon === faCircleNotch} />
            </span>
            <p className="truncate text-sm font-medium">{title}</p>
          </div>

          <p className="text-xs text-muted-foreground">
            {`${prettyPrintBytes(totalBytes * progress)} of ${prettyPrintBytes(totalBytes)} (${(
              progress * 100
            ).toFixed(1)}%)`}
          </p>

          <Progress value={progress * 100} />

          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="outline">{status}</Badge>
            <span className="inline-flex items-center gap-1">
              <FontAwesomeIcon icon={faUserGroup} /> {seeders + leechers}
            </span>
            <span className="inline-flex items-center gap-1 text-emerald-500">
              <FontAwesomeIcon icon={faSeedling} /> {seeders}
            </span>
            <span className="inline-flex items-center gap-1">
              <FontAwesomeIcon icon={faDownLong} /> {prettyPrintBytes(downloadRate)}/s
            </span>
            <span className="inline-flex items-center gap-1">
              <FontAwesomeIcon icon={faUpLong} /> {prettyPrintBytes(uploadRate)}/s
            </span>
          </div>
        </div>
      </div>
    </Card>
  );
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
