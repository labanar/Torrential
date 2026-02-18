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
import styles from "./torrent.module.css";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleArrowDown,
  faCircleArrowUp,
  faCircleNotch,
  faCirclePause,
  faDownLong,
  faPause,
  faPlay,
  faPlus,
  faSeedling,
  faTrash,
  faUpLong,
  faUserGroup,
} from "@fortawesome/free-solid-svg-icons";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSelector } from "react-redux";
import classNames from "classnames";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import {
  selectTorrentsByInfoHashes,
  torrentsWithPeersSelector,
  useAppDispatch,
} from "../../store";
import { TorrentsState, setTorrents } from "../../store/slices/torrentsSlice";
import {
  addTorrent,
  browseDirectories,
  PeerApiModel,
  previewTorrent,
  TorrentApiModel,
  TorrentPreviewApiModel,
} from "../../services/api";
import { PeerSummary, TorrentPreviewSummary, TorrentSummary } from "../../types";
import { setPeers } from "../../store/slices/peersSlice";
import {
  selectTorrentForDetail,
} from "../../store/slices/torrentDetailSlice";
import {
  FileUpload,
  FileUploadElement,
} from "../../components/FileUpload/file-upload";
import Layout from "../layout";
import { AlfredContext, setContext } from "../../store/slices/alfredSlice";
import { DetailPane } from "./detail-pane";

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

  const memoActionRow = useMemo(() => {
    return (
      <ActionsRow
        selectedTorrents={selectedTorrents}
        setSelectedTorrents={setSelectedTorrents}
      />
    );
  }, [selectedTorrents]);

  useEffect(() => {
    dispatch(setContext(AlfredContext.TorrentList));
  }, [dispatch]);

  const { enableScope } = useHotkeysContext();
  useEffect(() => {
    enableScope("torrents");
  }, [enableScope]);

  useEffect(() => {
    console.log("TORRENTS CHANGED");
  }, [torrents]);

  const selectTorrent = (infoHash: string) => {
    if (selectedTorrents.includes(infoHash)) {
      // If item is already selected, deselect it
      setSelectedTorrents(selectedTorrents.filter((item) => item !== infoHash));
    } else {
      // Add the item to the selected items
      setSelectedTorrents([...selectedTorrents, infoHash]);
    }
  };

  const isSelected = (infoHash: string) => {
    return selectedTorrents.includes(infoHash);
  };

  const fetchTorrents = useCallback(async () => {
    try {
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/torrents`
      );

      if (!response.ok) {
        console.log("Error fetching torrents");
        return;
      }

      const result = await response.json();
      if (result.data) {
        const { data } = result;

        const mappedTorrents = data.reduce(
          (pv: TorrentsState, cv: TorrentApiModel) => {
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
          },
          {}
        );
        dispatch(setTorrents(mappedTorrents));

        data.forEach((torrent: TorrentApiModel) => {
          const torrentPeers: PeerSummary[] = torrent.peers.reduce(
            (tpv: PeerSummary[], p: PeerApiModel) => {
              let summary: PeerSummary = {
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

          dispatch(
            setPeers({ infoHash: torrent.infoHash, peers: torrentPeers })
          );
        });
      }
    } catch (error) {
      console.log("error fetching torrents");
    }
  }, [dispatch]);

  useEffect(() => {
    fetchTorrents();
  }, [fetchTorrents]);

  useHotkeys(
    "up",
    () => {
      let nextId = currentPosition - 1;
      if (nextId < 0) nextId = torrents.length - 1;
      setCurrentPosition(nextId);
      console.log(nextId);
      console.log("up from torrents " + nextId);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    () => {
      let nextId = currentPosition + 1;
      if (nextId >= torrents.length) nextId = 0;
      setCurrentPosition(nextId);
      console.log(nextId);
      console.log("down from torrents " + nextId);
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "space",
    () => {
      selectTorrent(torrents[currentPosition].infoHash);
      console.log("space from torrents");
    },
    {
      scopes: ["torrents"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  // Track focused torrent for the detail pane
  const focusedInfoHash = torrents[currentPosition]?.infoHash ?? null;

  useEffect(() => {
    dispatch(selectTorrentForDetail(focusedInfoHash));
  }, [dispatch, focusedInfoHash]);

  return (
    <TooltipProvider>
      <div className={styles.root}>
        <div className={styles.topPane}>
          {memoActionRow}
          <div className={styles.torrentDivider}>
            <Separator orientation="horizontal" />
          </div>
          <div className={styles.torrentList}>
            {torrents.map((t, i) => (
              <TorrentRow
                toggleSelect={selectTorrent}
                toggleFocus={() => setCurrentPosition(i)}
                isFocused={currentPosition === i}
                isSelected={isSelected(t.infoHash)}
                uploadRate={t.uploadRate}
                downloadRate={t.downloadRate}
                status={t.status}
                key={t.infoHash}
                progress={t.progress ?? 0}
                infoHash={t.infoHash ?? ""}
                seeders={
                  t.peers?.reduce((pv, cv) => {
                    if (cv.isSeed) return pv + 1;
                    return pv;
                  }, 0) ?? 0
                }
                leechers={
                  t.peers?.reduce((pv, cv) => {
                    if (!cv.isSeed) return pv + 1;
                    return pv;
                  }, 0) ?? 0
                }
                totalBytes={t.sizeInBytes ?? 0}
                title={t.name ?? "???"}
              />
            ))}
          </div>
        </div>
        {focusedInfoHash && (
          <div className={styles.bottomPane}>
            <DetailPane infoHash={focusedInfoHash} />
          </div>
        )}
      </div>
    </TooltipProvider>
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
      await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/stop`,
        { method: "POST" }
      );
    } catch (e) {
      console.log(e);
      console.log("Error stopping torrent");
    }
  };

  const startTorrent = async (infoHash: string) => {
    try {
      await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/start`,
        { method: "POST" }
      );
    } catch (e) {
      console.log(e);
      console.log("Error stopping torrent");
    }
  };

  const removeTorrent = async (infoHash: string, deleteFiles: boolean) => {
    try {
      let body = {
        deleteFiles,
      };

      await fetch(
        `${import.meta.env.VITE_API_BASE_URL}/torrents/${infoHash}/delete`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(body),
        }
      );
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
    <div className={styles.actionBar}>
      <FileUpload
        accept=".torrent"
        ref={uploadRef}
        onFileChange={(f) => onUpload(f)}
      />

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

      <div className={styles.actionSearch}>
        <Input
          placeholder="Filter"
          style={{ maxWidth: "200px", justifySelf: "start" }}
        />
        {previewError && (
          <p className={styles.uploadError}>
            {previewError}
          </p>
        )}
      </div>

      <div className={styles.actionButtons}>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              size="icon"
              variant="outline"
              disabled={torrentActionsDisabled}
              className="border-green-600 text-green-600 hover:bg-green-600/10"
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
      <div style={{ flexGrow: 1, flexShrink: 1 }}></div>
    </div>
  );
};

interface TorrentFilePreviewModalProps {
  open: boolean;
  preview: TorrentPreviewSummary | null;
  selectedFileIds: number[];
  isAddLoading: boolean;
  addError: string | null;
  completedPathOverride: string;
  onCompletedPathChange: (value: string) => void;
  onToggleFile: (id: number) => void;
  onToggleAllFiles: () => void;
  onConfirm: () => void;
  onClose: () => void;
}

function TorrentFilePreviewModal({
  open,
  preview,
  selectedFileIds,
  isAddLoading,
  addError,
  completedPathOverride,
  onCompletedPathChange,
  onToggleFile,
  onToggleAllFiles,
  onConfirm,
  onClose,
}: TorrentFilePreviewModalProps) {
  const prettyPrintBytes = (bytes: number) => {
    const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
    if (bytes === 0) return "0 Byte";
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    if (i === 0) return bytes + " " + sizes[i];
    return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
  };

  const totalFiles = preview?.files.length ?? 0;
  const hasSomeSelected = selectedFileIds.length > 0;
  const hasAllSelected = totalFiles > 0 && selectedFileIds.length === totalFiles;
  const [isPathPickerOpen, setIsPathPickerOpen] = useState(false);
  const [isPathPickerLoading, setIsPathPickerLoading] = useState(false);
  const [pathPickerError, setPathPickerError] = useState<string | null>(null);
  const [pathPickerCurrentPath, setPathPickerCurrentPath] = useState("");
  const [pathPickerParentPath, setPathPickerParentPath] = useState<string | null>(null);
  const [pathPickerDirectories, setPathPickerDirectories] = useState<string[]>([]);
  const [pathPickerSelection, setPathPickerSelection] = useState("");

  const loadDirectories = useCallback(
    async (path?: string) => {
      setIsPathPickerLoading(true);
      setPathPickerError(null);

      try {
        const result = await browseDirectories(path);
        setPathPickerCurrentPath(result.currentPath);
        setPathPickerParentPath(result.parentPath ?? null);
        setPathPickerDirectories(result.directories);
        setPathPickerSelection(result.currentPath || "");
      } catch (error) {
        console.error("Error browsing directories:", error);
        setPathPickerError("Failed to load directories.");
      } finally {
        setIsPathPickerLoading(false);
      }
    },
    []
  );

  const openPathPicker = async () => {
    setIsPathPickerOpen(true);
    await loadDirectories(completedPathOverride.trim() || undefined);
  };

  const applySelectedPath = () => {
    if (!pathPickerSelection) {
      return;
    }

    onCompletedPathChange(pathPickerSelection);
    setIsPathPickerOpen(false);
  };

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !nextOpen && onClose()}>
      <DialogContent className={`max-w-3xl ${styles.deleteModal}`}>
        <DialogHeader>
          <DialogTitle>Choose Files to Download</DialogTitle>
        </DialogHeader>
        <div className={styles.previewModalBody}>
          <p className={styles.previewTorrentName}>{preview?.name}</p>
          <p className={styles.previewSelectionSummary}>
            {preview
              ? `${selectedFileIds.length} of ${preview.files.length} selected`
              : ""}
          </p>
          <label className={styles.checkboxLabel}>
            <Checkbox
              className={styles.previewSelectAll}
              checked={hasSomeSelected && !hasAllSelected ? "indeterminate" : hasAllSelected}
              onCheckedChange={() => onToggleAllFiles()}
              disabled={totalFiles === 0}
            />
            <span>{hasAllSelected ? "Deselect All" : "Select All"}</span>
          </label>
          <div className={styles.previewFileList}>
            {preview?.files.map((file) => (
              <div key={file.id} className={styles.previewFileRow}>
                <label className={styles.checkboxLabel}>
                  <Checkbox
                    checked={selectedFileIds.includes(file.id)}
                    onCheckedChange={() => onToggleFile(file.id)}
                  />
                  <span className={styles.previewFileName}>{file.filename}</span>
                </label>
                <span className={styles.previewFileSize}>
                  {prettyPrintBytes(file.sizeBytes)}
                </span>
              </div>
            ))}
          </div>
          <div className={styles.completedPathSection}>
            <p className={styles.completedPathLabel}>Completed Path</p>
            <div className={styles.completedPathInputRow}>
              <Input
                placeholder="Leave empty to use default"
                value={completedPathOverride}
                onChange={(e) => onCompletedPathChange(e.target.value)}
              />
              <Button size="sm" onClick={openPathPicker} type="button">
                Browse
              </Button>
            </div>
            <p className={styles.completedPathHint}>
              Override where files are moved after download completes.
            </p>
          </div>
          {addError && <p className={styles.uploadError}>{addError}</p>}

          <Dialog
            open={isPathPickerOpen}
            onOpenChange={(nextOpen) => setIsPathPickerOpen(nextOpen)}
          >
            <DialogContent className="max-w-3xl">
              <DialogHeader>
                <DialogTitle>Select Completed Path</DialogTitle>
              </DialogHeader>
              <div className={styles.pathPickerBody}>
                <div className={styles.pathPickerTopRow}>
                  <p className={styles.pathPickerCurrentPath}>
                    {pathPickerCurrentPath || "Choose a root folder"}
                  </p>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => loadDirectories(pathPickerParentPath ?? undefined)}
                    disabled={!pathPickerParentPath || isPathPickerLoading}
                    type="button"
                  >
                    Up
                  </Button>
                </div>
                <div className={styles.pathPickerList}>
                  {isPathPickerLoading && (
                    <p className={styles.pathPickerStatus}>Loading directories...</p>
                  )}
                  {!isPathPickerLoading && pathPickerDirectories.length === 0 && (
                    <p className={styles.pathPickerEmpty}>No child directories.</p>
                  )}
                  {!isPathPickerLoading &&
                    pathPickerDirectories.map((directory) => (
                      <Button
                        key={directory}
                        className="justify-start"
                        variant={pathPickerSelection === directory ? "default" : "ghost"}
                        onClick={() => setPathPickerSelection(directory)}
                        onDoubleClick={() => loadDirectories(directory)}
                        type="button"
                      >
                        {directory}
                      </Button>
                    ))}
                </div>
                {pathPickerError && <p className={styles.uploadError}>{pathPickerError}</p>}
              </div>
              <DialogFooter className={styles.dialogFooterActions}>
                <Button
                  variant="outline"
                  onClick={() => loadDirectories(pathPickerSelection || undefined)}
                  disabled={!pathPickerSelection || isPathPickerLoading}
                  type="button"
                >
                  Open
                </Button>
                <Button
                  onClick={applySelectedPath}
                  disabled={!pathPickerSelection}
                  type="button"
                >
                  Use Selected
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => setIsPathPickerOpen(false)}
                  type="button"
                >
                  Cancel
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
        <DialogFooter className={styles.dialogFooterActions}>
          <Button loading={isAddLoading} onClick={onConfirm} type="button">
            Add Torrent
          </Button>
          <Button variant="secondary" onClick={onClose} type="button">
            Cancel
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

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
  const color = useMemo(() => {
    if (status === "Stopped" || status === "Idle") return "orange";
    if (status === "Running") return "green";
    if (status === "Verifying" || status === "Copying") return "blue";
    return "gray";
  }, [status]);

  function prettyPrintBytes(bytes: number) {
    const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
    if (bytes === 0) return "0 Byte";
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    if (i === 0) return bytes + " " + sizes[i]; // For bytes, no decimal
    return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
  }

  const className = useMemo(() => {
    if (!isFocused) return classNames(styles.torrentContainer);

    return classNames(styles.torrentContainer, styles.torrentContainerSelected);
  }, [isFocused]);

  return (
    <div className={className} onClick={() => toggleFocus()}>
      <div className={styles.torrent}>
        <div className={styles.torrentCheckbox}>
          <Checkbox checked={isSelected} onCheckedChange={() => toggleSelect(infoHash)} />
        </div>
        <div className={styles.torrentInfo} key={infoHash}>
          <div className={styles.torrentInfoTitleRow}>
            <div className={styles.torrentIcon}>
              {status === "Running" && progress < 1 && (
                <FontAwesomeIcon
                  icon={faCircleArrowDown}
                  size={"1x"}
                  style={{
                    paddingRight: "0.8em",
                    paddingLeft: "0.4em",
                    color,
                  }}
                />
              )}
              {status === "Running" && progress >= 1 && (
                <FontAwesomeIcon
                  icon={faCircleArrowUp}
                  size={"1x"}
                  style={{
                    paddingRight: "0.8em",
                    paddingLeft: "0.4em",
                    color,
                  }}
                />
              )}
              {(status === "Stopped" || status === "Idle") && (
                <FontAwesomeIcon
                  icon={faCirclePause}
                  size={"1x"}
                  style={{
                    paddingRight: "0.8em",
                    paddingLeft: "0.4em",
                    color,
                  }}
                />
              )}
              {(status === "Verifying" || status === "Copying") && (
                <FontAwesomeIcon
                  icon={faCircleNotch}
                  spin
                  size={"1x"}
                  style={{
                    paddingRight: "0.8em",
                    paddingLeft: "0.4em",
                    color,
                  }}
                />
              )}
            </div>
            <p className={styles.title}>{title}</p>
          </div>

          <p className={styles.progress}>
            {`${prettyPrintBytes(totalBytes * progress)} of ${prettyPrintBytes(
              totalBytes
            )} (${(progress * 100).toFixed(1)}%)`}
          </p>
          <Progress
            value={progress * 100}
            className={classNames(styles.progressBar, {
              [styles.progressBarGreen]: color === "green",
              [styles.progressBarOrange]: color === "orange",
              [styles.progressBarBlue]: color === "blue",
              [styles.progressBarGray]: color === "gray",
            })}
          />
          <p className={styles.progressDetails}>
            <span>{status}</span>
            {" | "}
            <Tooltip>
              <TooltipTrigger asChild>
              <span>
                <FontAwesomeIcon
                  icon={faUserGroup}
                  size={"sm"}
                  style={{ paddingRight: "0.3em" }}
                />
                {`${seeders + leechers}`}
              </span>
              </TooltipTrigger>
              <TooltipContent>{`${seeders + leechers} peers`}</TooltipContent>
            </Tooltip>
            {" | "}
            <Tooltip>
              <TooltipTrigger asChild>
              <span>
                <FontAwesomeIcon
                  icon={faSeedling}
                  size={"sm"}
                  style={{ paddingRight: "0.3em" }}
                />
                {seeders}
              </span>
              </TooltipTrigger>
              <TooltipContent>{`${seeders} seeders`}</TooltipContent>
            </Tooltip>
            <span>
              <FontAwesomeIcon
                icon={faDownLong}
                size={"sm"}
                style={{ paddingLeft: "1em", paddingRight: "0.3em" }}
              />
              {prettyPrintBytes(downloadRate) + "/s"}
            </span>
            <span>
              <FontAwesomeIcon
                icon={faUpLong}
                size={"sm"}
                style={{ paddingLeft: "0.5em", paddingRight: "0.3em" }}
              />
              {prettyPrintBytes(uploadRate) + "/s"}
            </span>
          </p>
        </div>
      </div>
      <div className={styles.torrentDivider}>
        <Separator orientation="horizontal" />
      </div>
    </div>
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
      <DialogContent className={`max-w-2xl ${styles.deleteModal}`}>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>Are you sure you want remove:</DialogDescription>
        </DialogHeader>
        <div className={styles.deleteModalBody}>
          <ul style={{ paddingLeft: "2em" }}>
            {infoHashes.map((hash) => {
              return (
                <li key={hash}>
                  <p>{torrents[hash].name}</p>
                </li>
              );
            })}
          </ul>
          <label className={styles.checkboxLabel}>
            <Checkbox
              className={styles.deleteFilesCheckbox}
              checked={deleteFiles}
              onCheckedChange={(checked) => setDeleteFiles(checked === true)}
            />
            <span>Delete Files on Disk</span>
          </label>
        </div>
        <DialogFooter className={styles.dialogFooterActions}>
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
