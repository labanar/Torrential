"use client";

import {
  Button,
  Container,
  IconButton,
  Link,
  Progress,
  Text,
  Tooltip,
  useColorMode,
} from "@chakra-ui/react";
import styles from "./page.module.css";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faDownLong,
  faPause,
  faPlay,
  faPlus,
  faSeedling,
  faStop,
  faTrash,
  faUpLong,
  faUserGroup,
} from "@fortawesome/free-solid-svg-icons";
import { MutableRefObject, useEffect, useMemo, useRef } from "react";
import { FileUpload, FileUploadElement } from "@/components/FileUpload";
import { useAppDispatch } from "./hooks";
import { TorrentsState, setTorrents } from "@/features/torrentsSlice";
import { PeerSummary, TorrentSummary } from "@/types";
import { PeerApiModel, TorrentApiModel } from "@/api/types";
import { setPeers } from "@/features/peersSlice";
import { useSelector } from "react-redux";
import { torrentsWithPeersSelector } from "./selectors";
import { useRouter } from "next/navigation";

export default function Home() {
  const dispatch = useAppDispatch();
  const torrents = useSelector(torrentsWithPeersSelector);

  const fetchTorrents = async () => {
    try {
      const response = await fetch(`http://localhost:5142/torrents`);

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

        data.forEach((torrent) => {
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
  };

  useEffect(() => {
    fetchTorrents();
  }, []);

  return (
    <>
      <div className={styles.root}>
        <ActionsRow />
        <div className={styles.torrentList}>
          {torrents.map((t) => (
            <TorrentRow
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
    </>
  );
}

// interface ActionsRowProps
// {
//   uploadRef: MutableRefObject<FileUploadElement>
// }

const ActionsRow = () => {


  const uploadRef = useRef<FileUploadElement | null>(null);
  const onUpload = async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);

    try {
      const response = await fetch(`http://localhost:5142/torrents/add`, {
        method: "POST",
        body: formData,
      });

      if (!response.ok) {
        throw new Error("File upload failed");
      }

      const result = await response.json();
      console.log("File uploaded successfully:", result);
      // Handle success
    } catch (error) {
      console.error("Error uploading file:", error);
      // Handle error
    }
  };

  return (
    <Container className={styles.actionBar}>
      <FileUpload
        accept=".torrent"
        ref={uploadRef}
        onFileChange={(f) => onUpload(f)}
      />

      <Tooltip label="Start">
        <IconButton
          colorScheme={"green"}
          aria-label="Start"
          icon={<FontAwesomeIcon  icon={faPlay} />}
        />
      </Tooltip>

      <Tooltip label="Stop">
        <IconButton
          colorScheme={"orange"}
          aria-label="Stop"
          icon={<FontAwesomeIcon icon={faPause} />}
        />
      </Tooltip>

      <Tooltip label="Delete">
        <IconButton
          colorScheme={"red"}
          aria-label="Remove"
          icon={<FontAwesomeIcon icon={faTrash} />}
        />
      </Tooltip>

      <Tooltip label="Add Torrent">
        <IconButton
          colorScheme={"blue"}
          aria-label="Add"
          icon={<FontAwesomeIcon icon={faPlus} />}
          onClick={() => {
            if (uploadRef === null || uploadRef.current === null) return;
            uploadRef.current.openFilePicker();
          }}
        />
      </Tooltip>
    </Container>
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
}: TorrentRowProps) {
  const color = useMemo(() => {
    if (status === "Stopped") return "orange";
    if (status === "Running") return "green";
  }, [status]);

  function prettyPrintBytes(bytes: number) {
    const sizes = ["Bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
    if (bytes === 0) return "0 Byte";
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    if (i === 0) return bytes + " " + sizes[i]; // For bytes, no decimal
    return (bytes / Math.pow(1024, i)).toFixed(2) + " " + sizes[i];
  }

  return (
    <div className={styles.torrent}>
      <div className={styles.torrentIcon}>
        {status === "Running" && progress < 1 && (
          <FontAwesomeIcon
            icon={faDownLong}
            size={"xl"}
            style={{
              paddingRight: "0.8em",
              paddingLeft: "0.4em",
              color,
            }}
          />
        )}
        {status === "Running" && progress >= 1 && (
          <FontAwesomeIcon
            icon={faUpLong}
            size={"xl"}
            style={{
              paddingRight: "0.8em",
              paddingLeft: "0.4em",
              color,
            }}
          />
        )}
        {status === "Stopped" && (
          <FontAwesomeIcon
            icon={faPause}
            size={"xl"}
            style={{
              paddingRight: "0.8em",
              paddingLeft: "0.4em",
              color,
            }}
          />
        )}
      </div>
      <div className={styles.torrentInfo} key={infoHash}>
        <Text className={styles.title} fontSize={"md"} noOfLines={1}>
          {title}
        </Text>
        <Text className={styles.progress} fontSize={"xs"}>
          {`${prettyPrintBytes(totalBytes * progress)} of ${prettyPrintBytes(
            totalBytes
          )} (${(progress * 100).toFixed(1)}%)`}
        </Text>
        <Progress value={progress * 100} colorScheme={color} color={'#1a1a1a'} height={"1em"} />
        <Text className={styles.progressDetails} fontSize={"xs"}>
          <Tooltip label={`${seeders + leechers} peers`}>
            <span>
              <FontAwesomeIcon
                icon={faUserGroup}
                size={"sm"}
                style={{ paddingRight: "0.3em" }}
              />
              {`${seeders + leechers}`}
            </span>
          </Tooltip>
          {` • `}
          <Tooltip label={`${seeders} seeders`}>
            <span>
              <FontAwesomeIcon
                icon={faSeedling}
                size={"sm"}
                style={{ paddingRight: "0.3em" }}
              />
              {seeders}
            </span>
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
        </Text>
      </div>
    </div>
  );
}
