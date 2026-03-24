import { useCallback, useEffect, useMemo, useState } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleNotch,
  faFile,
  faSeedling,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";
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
import { Progress } from "@/components/ui/progress";
import { cn } from "@/lib/utils";

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
      {/* Header with torrent name */}
      <div className="border-b border-border/50 px-4 pt-4 pb-3">
        <div className="flex items-start justify-between gap-2">
          <h3 className="text-sm font-semibold leading-snug">{detail.name}</h3>
          <Button variant="ghost" size="icon" className="h-6 w-6 shrink-0 text-muted-foreground hover:text-foreground" onClick={onClose} type="button">
            <FontAwesomeIcon icon={faXmark} className="text-xs" />
          </Button>
        </div>
      </div>

      {/* Tabs */}
      <div className="px-4 pt-2">
        <TabsList className="w-full justify-start gap-0 bg-transparent p-0 border-b border-border/30">
          <TabsTrigger value="info" className="rounded-none border-b-2 border-transparent px-3 py-2 text-xs font-medium data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary data-[state=active]:shadow-none">
            General
          </TabsTrigger>
          <TabsTrigger value="peers" className="rounded-none border-b-2 border-transparent px-3 py-2 text-xs font-medium data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary data-[state=active]:shadow-none">
            Peers
          </TabsTrigger>
          <TabsTrigger value="bitfield" className="rounded-none border-b-2 border-transparent px-3 py-2 text-xs font-medium data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary data-[state=active]:shadow-none">
            Bitfield
          </TabsTrigger>
          <TabsTrigger value="files" className="rounded-none border-b-2 border-transparent px-3 py-2 text-xs font-medium data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-primary data-[state=active]:shadow-none">
            Files
          </TabsTrigger>
        </TabsList>
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

function formatEta(bytesRemaining: number, downloadRate: number): string {
  if (downloadRate <= 0 || bytesRemaining <= 0) return "-";
  const seconds = bytesRemaining / downloadRate;
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m} min`;
  return `${Math.floor(seconds)}s`;
}

function InfoSection({ detail }: { detail: TorrentDetail }) {
  const seedTimeDisplay = formatSeededTime(detail.totalSeededSeconds);
  const seedTarget = detail.desiredSeedTimeDays != null ? `${detail.desiredSeedTimeDays} days` : "-";
  const pct = (detail.progress * 100).toFixed(1);
  const bytesRemaining = detail.totalSizeBytes - detail.bytesDownloaded;
  const eta = formatEta(bytesRemaining, detail.downloadRate);
  const peerCount = detail.peers.length;
  const seedCount = detail.peers.filter(p => p.isSeed).length;

  return (
    <ScrollArea className="h-full">
      <div className="space-y-4 pt-3">
        {/* Status section */}
        <div className="space-y-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Status</p>
          <div className="space-y-1.5">
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">Downloading {pct}%</span>
              <span className="text-muted-foreground">ETA: {eta}</span>
            </div>
            <Progress value={detail.progress * 100} className="h-1.5" />
          </div>
        </div>

        {/* Stats grid */}
        <div className="grid grid-cols-2 gap-3">
          <StatCard label="Speed" value={`${prettyBytes(detail.downloadRate)}/s`} />
          <StatCard label="ETA" value={eta} />
          <StatCard label="Peers" value={`${peerCount} connected of ${peerCount + seedCount}`} />
          <StatCard label="Pieces" value={`${detail.bitfield.haveCount} / ${detail.bitfield.pieceCount}`} />
        </div>

        {/* Download info */}
        <div className="space-y-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Download Info</p>
          <div className="space-y-2 rounded-md border border-border/30 bg-card/30 p-3">
            <InfoPair label="Download Path" value={detail.downloadPath || "-"} />
            <InfoPair label="Total Size" value={prettyBytes(detail.totalSizeBytes)} />
            <InfoPair label="Downloaded" value={prettyBytes(detail.bytesDownloaded)} />
            <InfoPair label="Uploaded" value={prettyBytes(detail.bytesUploaded)} />
            <InfoPair
              label="Seed Time"
              value={`${seedTimeDisplay} / ${seedTarget}`}
            />
            <InfoPair label="Date Added" value={formatDate(detail.dateAdded)} />
            <InfoPair
              label="Date Completed"
              value={detail.dateCompleted ? formatDate(detail.dateCompleted) : "In progress"}
            />
          </div>
        </div>
      </div>
    </ScrollArea>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-border/30 bg-card/30 p-2.5">
      <p className="text-[10px] font-medium uppercase tracking-wider text-muted-foreground">{label}</p>
      <p className="mt-0.5 text-sm font-medium">{value}</p>
    </div>
  );
}

function PeersSection({ peers }: { peers: TorrentDetailPeer[] }) {
  if (peers.length === 0) {
    return <EmptyState text="No connected peers" />;
  }

  return (
    <ScrollArea className="h-full">
      <div className="space-y-2 pt-3">
        <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
          {peers.length} connected peer{peers.length !== 1 ? "s" : ""}
        </p>
        {peers.map((peer) => (
          <div key={peer.peerId} className="rounded-md border border-border/30 bg-card/30 p-3 transition-colors hover:bg-card/50">
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <span className="font-mono text-xs">{peer.ipAddress}:{peer.port}</span>
                {peer.isSeed ? (
                  <Badge variant="secondary" className="gap-1 px-1.5 py-0 text-[10px]">
                    <FontAwesomeIcon icon={faSeedling} className="h-2.5 w-2.5 text-emerald-500" /> Seed
                  </Badge>
                ) : (
                  <Badge variant="outline" className="px-1.5 py-0 text-[10px]">Peer</Badge>
                )}
              </div>
              <span className="text-xs text-muted-foreground">{(peer.progress * 100).toFixed(1)}%</span>
            </div>
            <div className="mt-1.5 flex items-center gap-3 text-[10px] text-muted-foreground">
              <span>DL: {prettyBytes(peer.bytesDownloaded)}</span>
              <span>UL: {prettyBytes(peer.bytesUploaded)}</span>
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
    <div className="space-y-3 pt-3">
      <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">Storage Bitfield</p>
      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <Badge variant="outline" className="text-[10px]">Pieces: {bitfield.haveCount} / {bitfield.pieceCount}</Badge>
        <Badge variant="outline" className="text-[10px]">{pct}% complete</Badge>
      </div>
      <div className="flex flex-wrap gap-[2px] rounded-lg border border-border/30 bg-card/30 p-3">
        {buckets.map((bucket, i) => (
          <div
            key={i}
            className="h-1.5 w-1.5 rounded-sm bg-primary"
            style={{ opacity: Math.max(0.1, bucket.ratio) }}
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
      <div className="space-y-1.5 pt-3">
        <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
          Files ({files.length})
        </p>
        {files.map((file) => (
          <div
            key={file.id}
            className="flex items-center justify-between gap-3 rounded-md border border-border/30 bg-card/30 px-3 py-2.5 transition-colors hover:bg-card/50"
          >
            <label className="flex min-w-0 items-center gap-2.5">
              <Checkbox
                checked={file.isSelected}
                disabled={pending}
                onCheckedChange={() => toggleFile(file.id, file.isSelected)}
              />
              <FontAwesomeIcon icon={faFile} className="h-3 w-3 shrink-0 text-muted-foreground" />
              <span className="truncate text-xs">{file.filename}</span>
            </label>
            <span className="shrink-0 text-[10px] text-muted-foreground">{prettyBytes(file.size)}</span>
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
    <div className="flex items-baseline justify-between gap-2">
      <p className="text-[10px] text-muted-foreground">{label}</p>
      <p className="text-right text-xs">{value}</p>
    </div>
  );
}
