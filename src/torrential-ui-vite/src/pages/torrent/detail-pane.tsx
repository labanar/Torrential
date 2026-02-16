import { useCallback, useEffect, useMemo, useState } from "react";
import { Checkbox, Spinner, Text } from "@chakra-ui/react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSeedling } from "@fortawesome/free-solid-svg-icons";
import classNames from "classnames";
import { useAppDispatch, useAppSelector } from "../../store";
import {
  fetchDetailStart,
  fetchDetailSuccess,
  fetchDetailError,
} from "../../store/slices/torrentDetailSlice";
import {
  fetchTorrentDetail,
  updateFileSelection,
} from "../../services/api";
import type { TorrentDetail, TorrentDetailPeer, TorrentFile } from "../../types";
import styles from "./detail-pane.module.css";

type Tab = "peers" | "bitfield" | "files";

export function DetailPane({ infoHash }: { infoHash: string }) {
  const dispatch = useAppDispatch();
  const { detail, loading, error } = useAppSelector((s) => s.torrentDetail);
  const [activeTab, setActiveTab] = useState<Tab>("peers");

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
          peers: data.peers.map((p) => ({
            peerId: p.peerId,
            ipAddress: p.ipAddress,
            port: p.port,
            bytesDownloaded: p.bytesDownloaded,
            bytesUploaded: p.bytesUploaded,
            isSeed: p.isSeed,
            progress: p.progress,
          })),
          bitfield: data.bitfield,
          files: data.files.map((f) => ({
            id: f.id,
            filename: f.filename,
            size: f.size,
            isSelected: f.isSelected,
          })),
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
      <div className={styles.detailPane}>
        <div className={styles.emptyState}>
          <Spinner size="sm" mr={2} />
          <Text>Loading details...</Text>
        </div>
      </div>
    );
  }

  if (error && !detail) {
    return (
      <div className={styles.detailPane}>
        <div className={styles.emptyState}>
          <Text>{error}</Text>
        </div>
      </div>
    );
  }

  if (!detail) return null;

  return (
    <div className={styles.detailPane}>
      <div className={styles.detailHeader}>
        <span className={styles.detailHeaderTitle}>{detail.name}</span>
      </div>
      <div className={styles.tabs}>
        <TabButton label="PEERS" tab="peers" active={activeTab} onClick={setActiveTab} />
        <TabButton label="BITFIELD" tab="bitfield" active={activeTab} onClick={setActiveTab} />
        <TabButton label="FILES" tab="files" active={activeTab} onClick={setActiveTab} />
      </div>
      <div className={styles.tabContent}>
        {activeTab === "peers" && <PeersSection peers={detail.peers} />}
        {activeTab === "bitfield" && <BitfieldSection bitfield={detail.bitfield} />}
        {activeTab === "files" && (
          <FilesSection
            infoHash={infoHash}
            files={detail.files}
            onRefresh={loadDetail}
          />
        )}
      </div>
    </div>
  );
}

function TabButton({
  label,
  tab,
  active,
  onClick,
}: {
  label: string;
  tab: Tab;
  active: Tab;
  onClick: (t: Tab) => void;
}) {
  return (
    <div
      className={classNames(styles.tab, { [styles.tabActive]: active === tab })}
      onClick={() => onClick(tab)}
    >
      {label}
    </div>
  );
}

function prettyBytes(bytes: number) {
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  if (bytes === 0) return "0 B";
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  if (i === 0) return bytes + " " + sizes[i];
  return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
}

function PeersSection({ peers }: { peers: TorrentDetailPeer[] }) {
  if (peers.length === 0) {
    return (
      <div className={styles.emptyState}>
        <Text>No connected peers</Text>
      </div>
    );
  }

  return (
    <table className={styles.peersTable}>
      <thead>
        <tr>
          <th>IP</th>
          <th>Port</th>
          <th>Progress</th>
          <th>Downloaded</th>
          <th>Uploaded</th>
          <th>Seed</th>
        </tr>
      </thead>
      <tbody>
        {peers.map((peer) => (
          <tr key={peer.peerId}>
            <td>{peer.ipAddress}</td>
            <td>{peer.port}</td>
            <td>{(peer.progress * 100).toFixed(1)}%</td>
            <td>{prettyBytes(peer.bytesDownloaded)}</td>
            <td>{prettyBytes(peer.bytesUploaded)}</td>
            <td>
              {peer.isSeed && (
                <FontAwesomeIcon
                  icon={faSeedling}
                  size="sm"
                  style={{ color: "var(--chakra-colors-green-400)" }}
                />
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
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

      // Bounded sampling keeps rendering responsive even for very large torrents.
      return Array.from({ length: bucketCount }, (_, bucketIndex) => {
        const startPiece = Math.floor(
          (bucketIndex * bitfield.pieceCount) / bucketCount
        );
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
    <div className={styles.bitfieldContainer}>
      <div className={styles.bitfieldStats}>
        <span>
          Pieces: {bitfield.haveCount} / {bitfield.pieceCount} ({pct}%)
        </span>
        <span>{buckets.length} buckets</span>
      </div>
      <div className={styles.bitfieldGrid}>
        {buckets.map((bucket, i) => (
          <div
            key={i}
            className={classNames(
              styles.bitfieldPiece,
              bucket.ratio > 0 ? styles.bitfieldPieceHave : styles.bitfieldPieceMissing
            )}
            style={{ opacity: Math.max(0.2, bucket.ratio) }}
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
    // The SignalR FileSelectionChanged event will update the store,
    // but also refresh to get the latest state.
    onRefresh();
    setPending(false);
  };

  if (files.length === 0) {
    return (
      <div className={styles.emptyState}>
        <Text>No files</Text>
      </div>
    );
  }

  return (
    <table className={styles.filesTable}>
      <thead>
        <tr>
          <th style={{ width: "40px" }}></th>
          <th>Filename</th>
          <th>Size</th>
        </tr>
      </thead>
      <tbody>
        {files.map((file) => (
          <tr key={file.id}>
            <td>
              <Checkbox
                isChecked={file.isSelected}
                isDisabled={pending}
                onChange={() => toggleFile(file.id, file.isSelected)}
                size="sm"
              />
            </td>
            <td>{file.filename}</td>
            <td>{prettyBytes(file.size)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
