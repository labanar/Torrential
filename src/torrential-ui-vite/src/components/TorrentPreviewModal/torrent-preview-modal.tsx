import { useCallback, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { browseDirectories } from "../../services/api";
import { TorrentPreviewSummary } from "../../types";
import styles from "./torrent-preview-modal.module.css";

export interface TorrentFilePreviewModalProps {
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

export function TorrentFilePreviewModal({
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
      <DialogContent
        className={`max-w-3xl ${styles.deleteModal} ${styles.previewModalContent}`}
      >
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
            <DialogContent
              className={`max-w-3xl ${styles.pathPickerModalContent}`}
            >
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
