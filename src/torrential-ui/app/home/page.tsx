"use client";

import { Button, Input, Progress, Text, Tooltip } from "@chakra-ui/react";
import styles from "./page.module.css";
import { title } from "process";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleDown,
  faDownLong,
  faSeedling,
  faUserGroup,
} from "@fortawesome/free-solid-svg-icons";
import prettyBytes from "pretty-bytes";
import { useRef } from "react";
import { FileUpload, FileUploadElement } from "@/components/FileUpload";

export default function Home() {
  const uploaderRef = useRef<FileUploadElement | null>(null);

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
    <>
      <div className={styles.root}>
        <div className={styles.torrentList}>
          <TorrentRow
            progress={0.2}
            infoHash={"A"}
            seeders={27}
            leechers={3}
            totalBytes={2e9}
            title="Dune.Part.One.2021.Hybrid.2160p.UHD.BluRay.REMUX.DV.HDR10Plus.HEVC.TrueHD.7.1.Atmos-WiLDCAT.mkv"
          />
          <TorrentRow
            infoHash={"B"}
            title="ubuntu-21.10-desktop-amd64.iso"
            totalBytes={2e9}
            progress={0.9}
            seeders={100}
            leechers={40}
          />
          <TorrentRow
            infoHash={"C"}
            progress={0.3}
            totalBytes={2.4e9}
            seeders={20}
            leechers={5}
            title="Dune.Part.One.2021.Hybrid.2160p.UHD.BluRay.REMUX.DV.HDR10Plus.HEVC.TrueHD.7.1.Atmos-WiLDCAT.mkv"
          />
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
}

function TorrentRow({
  infoHash,
  title,
  progress,
  totalBytes,
  seeders,
  leechers,
}: TorrentRowProps) {
  return (
    <div className={styles.torrent}>
      <div className={styles.torrentIcon}>
        <FontAwesomeIcon
          icon={faDownLong}
          size={"xl"}
          style={{
            paddingRight: "0.8em",
            paddingLeft: "0.4em",
            color: "darkgreen",
          }}
        />
      </div>
      <div className={styles.torrentInfo} key={infoHash}>
        <Text className={styles.title} fontSize={"md"} noOfLines={1}>
          {title}
        </Text>
        <Text className={styles.progress} fontSize={"xs"}>
          {`${prettyBytes(totalBytes * progress)} of ${prettyBytes(
            totalBytes
          )} (${progress}%)`}
        </Text>
        <Progress value={progress * 100} colorScheme="green" height={"1em"} />
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
