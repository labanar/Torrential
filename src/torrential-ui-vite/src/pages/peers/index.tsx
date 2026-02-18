import { useCallback, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSeedling } from "@fortawesome/free-solid-svg-icons";
import Layout from "../layout";
import { selectAllConnectedPeers, useAppDispatch } from "../../store";
import { useSelector } from "react-redux";
import { TorrentsState, setTorrents } from "../../store/slices/torrentsSlice";
import { setPeers } from "../../store/slices/peersSlice";
import {
  fetchTorrents as fetchTorrentsApi,
  PeerApiModel,
  TorrentApiModel,
} from "../../services/api";
import { PeerSummary, TorrentSummary } from "../../types";
import styles from "./peers.module.css";

export default function PeersPage() {
  return (
    <Layout>
      <Page />
    </Layout>
  );
}

function Page() {
  const dispatch = useAppDispatch();
  const peers = useSelector(selectAllConnectedPeers);

  const hydrate = useCallback(async () => {
    try {
      const data = await fetchTorrentsApi();

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
    } catch (error) {
      console.log("error fetching torrents");
    }
  }, [dispatch]);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  if (peers.length === 0) {
    return (
      <div className={`${styles.root} page-shell`}>
        <div className={styles.emptyState}>
          <p>No connected peers</p>
        </div>
      </div>
    );
  }

  return (
    <div className={`${styles.root} page-shell`}>
      <div className={styles.header}>
        <h2 className={styles.title}>Peers</h2>
        <span className={styles.peerCount}>{peers.length} connected</span>
      </div>
      <div className={styles.tableWrap}>
        <table className={styles.peersTable}>
          <thead>
            <tr>
              <th>IP</th>
              <th>Port</th>
              <th>Torrent</th>
              <th>Seed</th>
            </tr>
          </thead>
          <tbody>
            {peers.map((peer) => (
              <tr key={`${peer.infoHash}-${peer.peerId}`}>
                <td>{peer.ip}</td>
                <td>{peer.port}</td>
                <td className={styles.torrentNameCell}>{peer.torrentName}</td>
                <td>
                  {peer.isSeed && (
                    <FontAwesomeIcon
                      icon={faSeedling}
                      size="sm"
                      className={styles.seedIcon}
                    />
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className={styles.peerCards}>
        {peers.map((peer) => (
          <div
            key={`${peer.infoHash}-${peer.peerId}`}
            className={styles.peerCard}
          >
            <div className={styles.cardRow}>
              <span className={styles.cardLabel}>IP</span>
              <span className={styles.cardValue}>{peer.ip}</span>
            </div>
            <div className={styles.cardRow}>
              <span className={styles.cardLabel}>Port</span>
              <span className={styles.cardValue}>{peer.port}</span>
            </div>
            <div className={styles.cardRow}>
              <span className={styles.cardLabel}>Torrent</span>
              <span className={styles.cardValue}>{peer.torrentName}</span>
            </div>
            <div className={styles.cardRow}>
              <span className={styles.cardLabel}>Seed</span>
              <span className={styles.cardValue}>
                {peer.isSeed ? (
                  <FontAwesomeIcon
                    icon={faSeedling}
                    size="sm"
                    className={styles.seedIcon}
                  />
                ) : (
                  "No"
                )}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
