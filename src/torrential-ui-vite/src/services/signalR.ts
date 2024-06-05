import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { addPeer, removePeer, updatePeer } from "../store/slices/peersSlice";
import {
  PeerBitfieldReceivedEvent,
  PeerConnectedEvent,
  PeerDisconnectedEvent,
  PieceVerifiedEvent,
  TorrentAddedEvent,
  TorrentRemovedEvent,
  TorrentStartedEvent,
  TorrentStatsEvent,
  TorrentStoppedEvent,
} from "./api";
import { PeerSummary } from "../types";
import { removeTorrent, updateTorrent } from "../store/slices/torrentsSlice";
import { queueNotification } from "../store/slices/notificationsSlice";
import {
  faCheckCircle,
  faPlayCircle,
  faPlusCircle,
  faStopCircle,
  faTrash,
} from "@fortawesome/free-solid-svg-icons";
import store from "../store";

export class SignalRService {
  private connection: HubConnection;

  constructor(private url: string) {
    this.connection = new HubConnectionBuilder()
      .withUrl(this.url)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.registerEvents();
  }

  private registerEvents(): void {
    this.connection.on("PeerConnected", (event: PeerConnectedEvent) => {
      const summary: PeerSummary = {
        ...event,
        isSeed: false,
      };
      store.dispatch(addPeer(summary));
    });

    this.connection.on("PeerDisconnected", (event: PeerDisconnectedEvent) => {
      const { infoHash, peerId } = event;
      const payload = {
        infoHash,
        peerId,
      };
      store.dispatch(removePeer(payload));
    });

    this.connection.on(
      "PeerBitfieldReceived",
      (event: PeerBitfieldReceivedEvent) => {
        store.dispatch(
          updatePeer({
            infoHash: event.infoHash,
            peerId: event.peerId,
            update: { isSeed: event.hasAllPieces },
          })
        );
      }
    );

    this.connection.on(`PieceVerified`, (event: PieceVerifiedEvent) => {
      const { infoHash, progress } = event;
      const payload = {
        infoHash,
        update: { progress: Number(progress.toFixed(3)) },
      };
      store.dispatch(updateTorrent(payload));
    });

    this.connection.on("TorrentStarted", (event: TorrentStartedEvent) => {
      const { infoHash } = event;
      const payload = {
        infoHash,
        update: { status: "Running" },
      };
      store.dispatch(updateTorrent(payload));

      const { torrents } = store.getState();
      const { name } = torrents[infoHash];
      store.dispatch(
        queueNotification({
          title: "Torrent Started",
          description: name,
          duration: 3500,
          isClosable: true,
          status: "success",
          icon: faPlayCircle,
        })
      );
    });

    this.connection.on("TorrentStopped", (event: TorrentStoppedEvent) => {
      const { infoHash } = event;
      const payload = {
        infoHash,
        update: { status: "Stopped", downloadRate: 0, uploadRate: 0 },
      };
      const { torrents } = store.getState();
      const { name } = torrents[infoHash];

      store.dispatch(updateTorrent(payload));
      store.dispatch(
        queueNotification({
          title: "Torrent Stopped",
          description: name,
          duration: 3500,
          isClosable: true,
          status: "success",
          icon: faStopCircle,
        })
      );
    });

    this.connection.on("TorrentRemoved", (event: TorrentRemovedEvent) => {
      const { infoHash } = event;
      const payload = {
        infoHash,
      };

      const { torrents } = store.getState();
      const { name } = torrents[infoHash];

      store.dispatch(removeTorrent(payload));
      store.dispatch(
        queueNotification({
          title: "Torrent Removed",
          description: name,
          duration: 3500,
          isClosable: true,
          status: "warning",
          icon: faTrash,
        })
      );
    });

    this.connection.on("TorrentCompleted", (event: TorrentRemovedEvent) => {
      const { infoHash } = event;
      const { torrents } = store.getState();
      const { name } = torrents[infoHash];

      store.dispatch(
        queueNotification({
          title: "Torrent Completed",
          description: name,
          duration: 3500,
          isClosable: true,
          status: "success",
          icon: faCheckCircle,
        })
      );
    });

    this.connection.on("TorrentStatsUpdated", (event: TorrentStatsEvent) => {
      const { infoHash, uploadRate, downloadRate } = event;
      const payload = {
        infoHash,
        update: { uploadRate, downloadRate },
      };
      store.dispatch(updateTorrent(payload));
    });

    this.connection.on("TorrentAdded", (event: TorrentAddedEvent) => {
      const { infoHash, name, totalSize } = event;

      const payload = {
        infoHash,
        update: {
          infoHash,
          name,
          sizeInBytes: totalSize,
          progress: 0,
          status: "Idle",
          bytesDownloaded: 0,
          bytesUploaded: 0,
          downloadRate: 0,
          uploadRate: 0,
        },
      };

      store.dispatch(updateTorrent(payload));
      store.dispatch(
        queueNotification({
          title: "Torrent Added",
          description: name,
          duration: 3500,
          isClosable: true,
          status: "success",
          icon: faPlusCircle,
        })
      );
    });
  }

  public async startConnection(): Promise<void> {
    try {
      await this.connection.start();
      console.log("SignalR connection successfully started.");
    } catch (error) {
      console.error("SignalR Connection Error:", error);
    }
  }

  public async stopConnection(): Promise<void> {
    await this.connection.stop();
    console.log("SignalR connection stopped.");
  }
}

export default SignalRService;
