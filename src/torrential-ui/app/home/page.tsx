"use client";

import { Button, Input, Progress, Text, Tooltip } from "@chakra-ui/react";
import styles from "./page.module.css";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleDown,
  faDownLong,
  faPause,
  faSeedling,
  faUpLong,
  faUserGroup,
} from "@fortawesome/free-solid-svg-icons";
import prettyBytes from "pretty-bytes";
import { useEffect, useMemo, useRef, useState } from "react";
import { FileUpload, FileUploadElement } from "@/components/FileUpload";
import { HubConnection } from "@microsoft/signalr";
import { createSignalRConnection } from "@/utils/signalr";
import { useAppDispatch } from "../hooks";
import { TorrentsState, setTorrents } from "@/features/torrentsSlice";
import { PeerSummary, TorrentSummary } from "@/types";
import { PeerApiModel, TorrentApiModel } from "@/api/types";
import { PeersState, addPeer, setPeers } from "@/features/peersSlice";
import { useSelector } from "react-redux";
import { torrentsWithPeersSelector } from "../selectors";

export default function Home() {
  const uploaderRef = useRef<FileUploadElement | null>(null);
  const dispatch = useAppDispatch();
  const torrents = useSelector(torrentsWithPeersSelector)

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
        
        const mappedTorrents = data.reduce((pv : TorrentsState, cv: TorrentApiModel) => {
          const summary : TorrentSummary = {
            name: cv.name,
            infoHash: cv.infoHash,
            progress: cv.progress,
            sizeInBytes: cv.totalSizeBytes,
            status: cv.status
          }

          pv[cv.infoHash] = summary;
          return pv;
        }, {});
        dispatch(setTorrents(mappedTorrents));

        data.forEach(torrent => {
          const torrentPeers : PeerSummary[] = torrent.peers.reduce((tpv : PeerSummary[], p: PeerApiModel) => {
            let summary : PeerSummary = {
              infoHash: torrent.infoHash,
              ip: p.ipAddress,
              port: p.port,
              peerId: p.peerId,
              isSeed: p.isSeed
            }

            tpv.push(summary)
            return tpv;
          }, []);

          dispatch(setPeers({infoHash: torrent.infoHash, peers: torrentPeers}))
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
        <div className={styles.torrentList}>
          {torrents.map((t) => (
            <TorrentRow
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
        <div className="actions">
          <FileUpload
            accept=".torrent"
            ref={uploaderRef}
            onFileChange={(f) => onUpload(f)}
          />
          <Button
            className={styles.fab}
            onClick={() => {
              if (uploaderRef === null || uploaderRef.current === null) return;
              uploaderRef.current.openFilePicker();
            }}
          >
            Hi
          </Button>
        </div>
      </div>
    </>
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
}

function TorrentRow({
  infoHash,
  title,
  progress,
  totalBytes,
  seeders,
  leechers,
  status
}: TorrentRowProps) {

  const color = useMemo(() => {
    if(status === "Stopped") return "orange";
    if(status === "Running") return "green"
  }, [status])

  function prettyPrintBytes(bytes : number) {
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
    if (bytes === 0) return '0 Byte';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    if (i === 0) return bytes + ' ' + sizes[i]; // For bytes, no decimal
    return (bytes / Math.pow(1024, i)).toFixed(2) + ' ' + sizes[i];
}

  return (
    <div className={styles.torrent}>
      <div className={styles.torrentIcon}>
        {status === "Running" && progress < 1 && (<FontAwesomeIcon
          icon={faDownLong}
          size={"xl"}
          style={{
            paddingRight: "0.8em",
            paddingLeft: "0.4em",
            color,
          }}
        />)}
         {status === "Running" && progress >= 1 && (<FontAwesomeIcon
          icon={faUpLong}
          size={"xl"}
          style={{
            paddingRight: "0.8em",
            paddingLeft: "0.4em",
            color,
          }}
        />)}
        {status === "Stopped" && (<FontAwesomeIcon
          icon={faPause}
          size={"xl"}
          style={{
            paddingRight: "0.8em",
            paddingLeft: "0.4em",
            color,
          }}
        />)}
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
        <Progress value={progress * 100} colorScheme={color} height={"1em"} />
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
        </Text>
      </div>
    </div>
  );
}
