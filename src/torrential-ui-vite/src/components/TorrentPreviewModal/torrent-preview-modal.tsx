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
import { ScrollArea } from "@/components/ui/scroll-area";
import { Label } from "@/components/ui/label";

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

  const loadDirectories = useCallback(async (path?: string) => {
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
  }, []);

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
      <DialogContent className="max-h-[calc(100dvh-1rem)] max-w-3xl overflow-hidden">
        <DialogHeader>
          <DialogTitle>Choose Files to Download</DialogTitle>
        </DialogHeader>

        <div className="grid gap-4">
          <div>
            <p className="truncate text-base font-semibold">{preview?.name}</p>
            <p className="text-sm text-muted-foreground">
              {preview ? `${selectedFileIds.length} of ${preview.files.length} selected` : ""}
            </p>
          </div>

          <Label className="flex items-center gap-2">
            <Checkbox
              checked={hasSomeSelected && !hasAllSelected ? "indeterminate" : hasAllSelected}
              onCheckedChange={() => onToggleAllFiles()}
              disabled={totalFiles === 0}
            />
            <span>{hasAllSelected ? "Deselect All" : "Select All"}</span>
          </Label>

          <ScrollArea className="max-h-[18rem] rounded-md border p-2">
            <div className="space-y-1">
              {preview?.files.map((file) => (
                <div
                  key={file.id}
                  className="flex items-center justify-between gap-3 rounded-md px-2 py-1.5 hover:bg-muted/60"
                >
                  <Label className="flex min-w-0 items-center gap-3 font-normal">
                    <Checkbox
                      checked={selectedFileIds.includes(file.id)}
                      onCheckedChange={() => onToggleFile(file.id)}
                    />
                    <span className="truncate">{file.filename}</span>
                  </Label>
                  <span className="text-xs text-muted-foreground">
                    {prettyPrintBytes(file.sizeBytes)}
                  </span>
                </div>
              ))}
            </div>
          </ScrollArea>

          <div className="grid gap-2">
            <Label>Completed Path</Label>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Input
                placeholder="Leave empty to use default"
                value={completedPathOverride}
                onChange={(e) => onCompletedPathChange(e.target.value)}
              />
              <Button size="sm" onClick={openPathPicker} type="button" className="sm:w-auto">
                Browse
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Override where files are moved after download completes.
            </p>
          </div>

          {addError && <p className="text-sm text-destructive">{addError}</p>}

          <Dialog open={isPathPickerOpen} onOpenChange={setIsPathPickerOpen}>
            <DialogContent className="max-h-[calc(100dvh-1rem)] max-w-3xl overflow-hidden">
              <DialogHeader>
                <DialogTitle>Select Completed Path</DialogTitle>
              </DialogHeader>
              <div className="grid gap-3">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <p className="truncate text-sm text-muted-foreground">
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

                <ScrollArea className="max-h-[18rem] rounded-md border p-2">
                  <div className="flex flex-col gap-1">
                    {isPathPickerLoading && (
                      <p className="text-sm text-muted-foreground">Loading directories...</p>
                    )}
                    {!isPathPickerLoading && pathPickerDirectories.length === 0 && (
                      <p className="text-sm text-muted-foreground">No child directories.</p>
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
                </ScrollArea>

                {pathPickerError && <p className="text-sm text-destructive">{pathPickerError}</p>}
              </div>
              <DialogFooter className="gap-2">
                <Button
                  variant="outline"
                  onClick={() => loadDirectories(pathPickerSelection || undefined)}
                  disabled={!pathPickerSelection || isPathPickerLoading}
                  type="button"
                >
                  Open
                </Button>
                <Button onClick={applySelectedPath} disabled={!pathPickerSelection} type="button">
                  Use Selected
                </Button>
                <Button variant="secondary" onClick={() => setIsPathPickerOpen(false)} type="button">
                  Cancel
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>

        <DialogFooter className="gap-2">
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
