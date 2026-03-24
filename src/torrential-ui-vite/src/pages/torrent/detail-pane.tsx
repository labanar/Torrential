import { useCallback, useEffect, useMemo, useState } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCircleNotch, faSeedling, faXmark } from "@fortawesome/free-solid-svg-icons";
import { useAppDispatch, useAppSelector } from "../../store";
import {
  fetchDetailStart,
  fetchDetailSuccess,
  fetchDetailError,
} from "../../store/slices/torrentDetailSlice";
import { fetchTorrentDetail, updateFileSelection } from "../../services/api";
import type { TorrentDetail, TorrentDetailPeer, TorrentFile } from "../../types";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Badge } from "@/components/ui/badge";

type Tab = "info" | "peers" | "bitfield" | "files";

interface DetailPaneProps {
  infoHash: string;
  onClose: () => void;
}

export function DetailPane({ infoHash, onClose }: DetailPaneProps) {
  const dispatch = useAppDispatch();
  const { detail, loading, error } = useAppSelector((s) => s.torrentDetail);
  const [activeTab, setActiveTab] = useState<Tab>("info");

  const loadDetail = useCallback(async () => {
    dispatch(fetchDetailStart());
    try {
      const data = await fetchTorrentDetail(infoHash);
      if (data) {
        const mapped: TorrentDetail = {
          infoHash: data.infoHash,
          name: data.name,
          status: data.status,
          progress: data.progress,
          totalSizeBytes: data.totalSizeBytes,
          bytesDownloaded: data.bytesDownloaded,
          bytesUploaded: data.bytesUploaded,
          downloadRate: data.downloadRate,
          uploadRate: data.uploadRate,
          peers: (data.peers ?? []).map((p) => ({
            peerId: p.peerId,
            ipAddress: p.ipAddress,
            port: p.port,
            bytesDownloaded: p.bytesDownloaded,
            bytesUploaded: p.bytesUploaded,
            isSeed: p.isSeed,
            progress: p.progress,
          })),
          bitfield: data.bitfield ?? { pieceCount: 0, haveCount: 0, bitfield: "" },
          files: (data.files ?? []).map((f) => ({
            id: f.id,
            filename: f.filename,
            size: f.size,
            isSelected: f.isSelected,
          })),
          downloadPath: data.downloadPath,
          dateAdded: data.dateAdded,
          dateCompleted: data.dateCompleted,
          dateFirstSeeded: data.dateFirstSeeded,
          desiredSeedTimeDays: data.desiredSeedTimeDays,
          totalSeededSeconds: data.totalSeededSeconds,
        };
        dispatch(fetchDetailSuccess(mapped));
      } else {
        dispatch(fetchDetailError("Failed to load torrent details"));
      }
    } catch {
      dispatch(fetchDetailError("Failed to load torrent details"));
    }
  }, [dispatch, infoHash]);

  useEffect(() => {
    loadDetail();
  }, [loadDetail]);

  if (loading && !detail) {
    return (
      <div className="flex h-full items-center justify-center p-6 text-muted-foreground">
        <FontAwesomeIcon icon={faCircleNotch} spin className="mr-2" />
        <p>Loading details...</p>
      </div>
    );
  }

  if (error && !detail) {
    return (
      <div className="flex h-full items-center justify-center p-6 text-muted-foreground">
        <p>{error}</p>
      </div>
    );
  }

  if (!detail) {
    return (
      <div className="flex h-full items-center justify-center p-6 text-muted-foreground">
        <p>No details available</p>
      </div>
    );
  }

  return (
    <Tabs value={activeTab} onValueChange={(value) => setActiveTab(value as Tab)} className="flex h-full min-h-0 flex-col overflow-hidden bg-background">
      <div className="flex items-center gap-2 px-4 py-2">
        <TabsList className="flex-1 justify-start bg-transparent p-0">
          <TabsTrigger value="info">Info</TabsTrigger>
          <TabsTrigger value="peers">Peers</TabsTrigger>
          <TabsTrigger value="bitfield">Bitfield</TabsTrigger>
          <TabsTrigger value="files">Files</TabsTrigger>
        </TabsList>
        <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={onClose} type="button">
          <FontAwesomeIcon icon={faXmark} className="text-sm" />
        </Button>
      </div>

      <div className="min-h-0 flex-1 px-4 pb-4">
        <TabsContent value="info" className="h-full mt-0">
          <InfoSection detail={detail} />
        </TabsContent>
        <TabsContent value="peers" className="h-full mt-0">
          <PeersSection peers={detail.peers} />
        </TabsContent>
        <TabsContent value="bitfield" className="h-full mt-0">
          <BitfieldSection bitfield={detail.bitfield} />
        </TabsContent>
        <TabsContent value="files" className="h-full mt-0">
          <FilesSection infoHash={infoHash} files={detail.files} onRefresh={loadDetail} />
        </TabsContent>
      </div>
    </Tabs>
  );
}

function prettyBytes(bytes: number) {
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  if (bytes === 0) return "0 B";
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  if (i === 0) return bytes + " " + sizes[i];
  return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
}

function formatSeededTime(totalSeconds: number): string {
  if (totalSeconds <= 0) return "0m";
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const parts: string[] = [];
  if (days > 0) parts.push(`${days}d`);
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0 || parts.length === 0) parts.push(`${minutes}m`);
  return parts.join(" ");
}

function formatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return "-";
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return "-";
  return date.toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function InfoSection({ detail }: { detail: TorrentDetail }) {
  const seedTimeDisplay = formatSeededTime(detail.totalSeededSeconds);
  const seedTarget = detail.desiredSeedTimeDays != null ? `${detail.desiredSeedTimeDays} days` : "-";

  return (
    <ScrollArea className="h-full">
      <div className="space-y-4 py-1">
        <div className="grid gap-3 sm:grid-cols-2">
          <InfoPair label="Download Path" value={detail.downloadPath || "-"} />
          <InfoPair
            label="Seed Time"
            value={`${seedTimeDisplay} / ${seedTarget}`}
          />
          <InfoPair label="Date Added" value={formatDate(detail.dateAdded)} />
          <InfoPair
            label="Date Completed"
            value={detail.dateCompleted ? formatDate(detail.dateCompleted) : "In progress"}
          />
          <InfoPair label="Total Size" value={prettyBytes(detail.totalSizeBytes)} />
          <InfoPair label="Total Downloaded" value={prettyBytes(detail.bytesDownloaded)} />
          <InfoPair label="Total Uploaded" value={prettyBytes(detail.bytesUploaded)} />
        </div>
      </div>
    </ScrollArea>
  );
}

function PeersSection({ peers }: { peers: TorrentDetailPeer[] }) {
  if (peers.length === 0) {
    return <EmptyState text="No connected peers" />;
  }

  return (
    <ScrollArea className="h-full">
      <div className="divide-y divide-border">
        {peers.map((peer) => (
          <div key={peer.peerId} className="px-3 py-3 transition-colors hover:bg-muted/50">
            <div className="grid gap-2 sm:grid-cols-3">
              <InfoPair label="Address" value={`${peer.ipAddress}:${peer.port}`} />
              <InfoPair label="Progress" value={`${(peer.progress * 100).toFixed(1)}%`} />
              <div className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Role</span>
                {peer.isSeed ? (
                  <Badge variant="secondary" className="gap-1">
                    <FontAwesomeIcon icon={faSeedling} className="text-emerald-500" /> Seed
                  </Badge>
                ) : (
                  <Badge variant="outline">Peer</Badge>
                )}
              </div>
              <InfoPair label="Downloaded" value={prettyBytes(peer.bytesDownloaded)} />
              <InfoPair label="Uploaded" value={prettyBytes(peer.bytesUploaded)} />
            </div>
          </div>
        ))}
      </div>
    </ScrollArea>
  );
}

function BitfieldSection({
  bitfield,
}: {
  bitfield: { pieceCount: number; haveCount: number; bitfield: string };
}) {
  const buckets = useMemo(() => {
    if (!bitfield.bitfield || bitfield.pieceCount <= 0) return [];
    try {
      const raw = atob(bitfield.bitfield);

      const bytes = new Uint8Array(raw.length);
      for (let i = 0; i < raw.length; i++) {
        bytes[i] = raw.charCodeAt(i);
      }

      const maxBuckets = 2048;
      const bucketCount = Math.min(bitfield.pieceCount, maxBuckets);

      const hasPiece = (pieceIndex: number) => {
        const byteIndex = pieceIndex >> 3;
        const bitOffset = 7 - (pieceIndex & 7);
        return ((bytes[byteIndex] >> bitOffset) & 1) === 1;
      };

      return Array.from({ length: bucketCount }, (_, bucketIndex) => {
        const startPiece = Math.floor((bucketIndex * bitfield.pieceCount) / bucketCount);
        const endPiece = Math.max(
          startPiece + 1,
          Math.floor(((bucketIndex + 1) * bitfield.pieceCount) / bucketCount)
        );
        const span = endPiece - startPiece;
        const sampleCount = Math.min(8, span);

        let haveSamples = 0;
        for (let sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++) {
          const sampledPiece =
            startPiece + Math.floor(((sampleIndex + 0.5) * span) / sampleCount);
          if (hasPiece(sampledPiece)) {
            haveSamples++;
          }
        }

        return {
          ratio: haveSamples / sampleCount,
          startPiece,
          endPiece,
        };
      });
    } catch {
      return [];
    }
  }, [bitfield.bitfield, bitfield.pieceCount]);

  const pct =
    bitfield.pieceCount > 0
      ? ((bitfield.haveCount / bitfield.pieceCount) * 100).toFixed(1)
      : "0.0";

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <Badge variant="outline">Pieces: {bitfield.haveCount} / {bitfield.pieceCount}</Badge>
        <Badge variant="outline">{pct}% complete</Badge>
        <Badge variant="outline">{buckets.length} buckets</Badge>
      </div>
      <div className="flex flex-wrap gap-[2px] rounded-md bg-muted/30 p-3">
        {buckets.map((bucket, i) => (
          <div
            key={i}
            className="h-1 w-1 rounded-sm bg-primary"
            style={{ opacity: Math.max(0.15, bucket.ratio) }}
            title={`Pieces ${bucket.startPiece} - ${bucket.endPiece - 1}`}
          />
        ))}
      </div>
    </div>
  );
}

function FilesSection({
  infoHash,
  files,
  onRefresh,
}: {
  infoHash: string;
  files: TorrentFile[];
  onRefresh: () => void;
}) {
  const [pending, setPending] = useState(false);

  const toggleFile = async (fileId: number, currentlySelected: boolean) => {
    setPending(true);
    const currentIds = files.filter((f) => f.isSelected).map((f) => f.id);
    let newIds: number[];
    if (currentlySelected) {
      newIds = currentIds.filter((id) => id !== fileId);
    } else {
      newIds = [...currentIds, fileId];
    }
    await updateFileSelection(infoHash, newIds);
    onRefresh();
    setPending(false);
  };

  if (files.length === 0) {
    return <EmptyState text="No files" />;
  }

  return (
    <ScrollArea className="h-full">
      <div className="divide-y divide-border">
        {files.map((file) => (
          <div
            key={file.id}
            className="flex items-center justify-between gap-3 px-3 py-3 transition-colors hover:bg-muted/50"
          >
            <label className="flex min-w-0 items-center gap-3">
              <Checkbox
                checked={file.isSelected}
                disabled={pending}
                onCheckedChange={() => toggleFile(file.id, file.isSelected)}
              />
              <span className="truncate text-sm">{file.filename}</span>
            </label>
            <span className="text-xs text-muted-foreground">{prettyBytes(file.size)}</span>
          </div>
        ))}
      </div>
    </ScrollArea>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className="flex h-full items-center justify-center p-8 text-sm text-muted-foreground">
      {text}
    </div>
  );
}

function InfoPair({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="truncate text-sm">{value}</p>
    </div>
  );
}
