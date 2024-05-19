"use client";

import {
  Button,
  Card,
  Container,
  Divider,
  IconButton,
  Input,
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
import { MutableRefObject, useEffect, useMemo, useRef, useState } from "react";
import { FileUpload, FileUploadElement } from "@/components/FileUpload";
import { useAppDispatch } from "./hooks";
import { TorrentsState, setTorrents } from "@/features/torrentsSlice";
import { PeerSummary, TorrentSummary } from "@/types";
import { PeerApiModel, TorrentApiModel } from "@/api/types";
import { setPeers } from "@/features/peersSlice";
import { useSelector } from "react-redux";
import { torrentsWithPeersSelector } from "./selectors";
import { useRouter } from "next/navigation";
import classNames from "classnames";

export default function Home() {
  const dispatch = useAppDispatch();
  const torrents = useSelector(torrentsWithPeersSelector);
  const [selectedTorrents, setSelectedTorrents] = useState<string[]>([]);

  const selectTorrent = (infoHash: string, event: React.MouseEvent) => {
    if (event.ctrlKey || event.metaKey) {
      // Multi-select with Ctrl or Cmd key
      if (selectedTorrents.includes(infoHash)) {
        // If item is already selected, deselect it
        setSelectedTorrents(
          selectedTorrents.filter((item) => item !== infoHash)
        );
      } else {
        // Add the item to the selected items
        setSelectedTorrents([...selectedTorrents, infoHash]);
      }
    } else {
      // Single select or deselect if already selected
      if (selectedTorrents.includes(infoHash)) {
        setSelectedTorrents([]);
      } else {
        setSelectedTorrents([infoHash]);
      }
    }
  };

  const isSelected = (infoHash: string) => {
    return selectedTorrents.includes(infoHash);
  };


  
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
        <ActionsRow selectedTorrents={selectedTorrents} />
        <div className={styles.torrentDivider}>
          <Divider orientation="horizontal" />
        </div>
        <div className={styles.torrentList}>
          {torrents.map((t) => (
            <>
              <TorrentRow
                onSelect={selectTorrent}
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
            </>
          ))}
        </div>
      </div>
    </>
  );
}

interface ActionsRowProps {
  selectedTorrents: string[];
}

const ActionsRow = ({ selectedTorrents }: ActionsRowProps) => {
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

  const stopTorrent = async (infoHash: string) => {
    try {
      const response = await fetch(`http://localhost:5142/torrents/${infoHash}/stop`, {method: 'POST'})
    }
    catch(e) {
      console.log(e);
      console.log("Error stopping torrent")
    }
  }

  const startTorrent = async (infoHash: string) => {
    try {
      const response = await fetch(`http://localhost:5142/torrents/${infoHash}/start`, {method: 'POST'})
    }
    catch(e) {
      console.log(e);
      console.log("Error stopping torrent")
    }
  }


  const stopTorrents = async () => {
    selectedTorrents.forEach( infoHash => {
       stopTorrent(infoHash);
    });
  }

  const startTorrents = async () => {
    selectedTorrents.forEach( infoHash => {
      startTorrent(infoHash);
   }); 
  }


  const torrentActionsDisabled = useMemo(() => {
    return selectedTorrents.length === 0;
  }, [selectedTorrents]);

  return (
    <div className={styles.actionBar}>
      <FileUpload
        accept=".torrent"
        ref={uploadRef}
        onFileChange={(f) => onUpload(f)}
      />

      <div className={styles.actionSearch}>
        <Input
          variant={"filed"}
          placeholder="Filter"
          style={{ maxWidth: "200px", justifySelf: "start" }}
        />
      </div>

      <div className={styles.actionButtons}>
        <Tooltip label="Start">
          <IconButton
            isDisabled={torrentActionsDisabled}
            onClick={() => startTorrents()}
            colorScheme={"green"}
            aria-label="Start"
            icon={<FontAwesomeIcon icon={faPlay} />}
          />
        </Tooltip>

        <Tooltip label="Stop">
          <IconButton
            onClick={() => stopTorrents()}
            isDisabled={torrentActionsDisabled}
            colorScheme={"orange"}
            aria-label="Stop"
            icon={<FontAwesomeIcon icon={faPause} />}
          />
        </Tooltip>

        <Tooltip label="Delete">
          <IconButton
            isDisabled={torrentActionsDisabled}
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
      </div>
      <div style={{ flexGrow: 1, flexShrink: 1 }}></div>
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
  isSelected: boolean;
  onSelect: (innfoHash: string, e: React.MouseEvent) => void;
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
  isSelected,
  onSelect,
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

  const className = useMemo(() => {
    if (!isSelected) return classNames(styles.torrentContainer);

    return classNames(styles.torrentContainer, styles.torrentContainerSelected);
  }, [isSelected]);

  return (
    <div className={className} onClick={(e) => onSelect(infoHash, e)}>
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
          <Progress
            value={progress * 100}
            colorScheme={color}
            color={"#1a1a1a"}
            height={"1em"}
          />
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
      <div className={styles.torrentDivider}>
        <Divider orientation="horizontal" />
      </div>
    </div>
  );
}
