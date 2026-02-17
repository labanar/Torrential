import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { addPeer, removePeer, updatePeer } from "../store/slices/peersSlice";
import {
  FileSelectionChangedEvent,
  PeerBitfieldReceivedEvent,
  PeerConnectedEvent,
  PeerDisconnectedEvent,
  PieceVerifiedEvent,
  TorrentAddedEvent,
  TorrentFileCopyCompletedEvent,
  TorrentFileCopyStartedEvent,
  TorrentRemovedEvent,
  TorrentStartedEvent,
  TorrentVerificationCompletedEvent,
  TorrentVerificationStartedEvent,
  TorrentStatsEvent,
  TorrentStoppedEvent,
} from "./api";
import { PeerSummary } from "../types";
import { removeTorrent, updateTorrent } from "../store/slices/torrentsSlice";
import { queueNotification } from "../store/slices/notificationsSlice";
import {
  applyVerifiedPiecesToDetailBitfield,
  updateDetailBitfield,
  updateDetailFiles,
} from "../store/slices/torrentDetailSlice";
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
  private fileCopyInFlightByTorrent: Record<string, number> = {};
  private preCopyStatusByTorrent: Record<string, string> = {};

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

      // Apply incremental piece updates to detail pane if this torrent is selected.
      const { torrentDetail } = store.getState();
      if (torrentDetail.selectedInfoHash === infoHash && torrentDetail.detail) {
        if (event.verifiedPieces && event.verifiedPieces.length > 0) {
          store.dispatch(
            applyVerifiedPiecesToDetailBitfield({
              verifiedPieces: event.verifiedPieces,
            })
          );
        } else {
          store.dispatch(
            updateDetailBitfield({
              haveCount: Math.min(
                torrentDetail.detail.bitfield.pieceCount,
                torrentDetail.detail.bitfield.haveCount + 1
              ),
              bitfield: torrentDetail.detail.bitfield.bitfield,
            })
          );
        }
      }
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

    this.connection.on(
      "TorrentVerificationStarted",
      (event: TorrentVerificationStartedEvent) => {
        store.dispatch(
          updateTorrent({
            infoHash: event.infoHash,
            update: { status: "Verifying" },
          })
        );
      }
    );

    this.connection.on(
      "TorrentVerificationCompleted",
      (event: TorrentVerificationCompletedEvent) => {
        const { torrents } = store.getState();
        const currentStatus = torrents[event.infoHash]?.status;
        if (currentStatus === "Verifying") {
          store.dispatch(
            updateTorrent({
              infoHash: event.infoHash,
              update: { status: "Idle" },
            })
          );
        }
      }
    );

    this.connection.on(
      "TorrentFileCopyStarted",
      (event: TorrentFileCopyStartedEvent) => {
        const { torrents } = store.getState();
        const currentStatus = torrents[event.infoHash]?.status ?? "Idle";

        const inFlightCopies = this.fileCopyInFlightByTorrent[event.infoHash] ?? 0;
        this.fileCopyInFlightByTorrent[event.infoHash] = inFlightCopies + 1;

        if (inFlightCopies === 0) {
          this.preCopyStatusByTorrent[event.infoHash] = currentStatus;
        }

        store.dispatch(
          updateTorrent({
            infoHash: event.infoHash,
            update: { status: "Copying" },
          })
        );
      }
    );

    this.connection.on(
      "TorrentFileCopyCompleted",
      (event: TorrentFileCopyCompletedEvent) => {
        const inFlightCopies = this.fileCopyInFlightByTorrent[event.infoHash] ?? 0;
        if (inFlightCopies <= 0) return;

        const remainingCopies = inFlightCopies - 1;
        this.fileCopyInFlightByTorrent[event.infoHash] = remainingCopies;
        if (remainingCopies > 0) return;

        delete this.fileCopyInFlightByTorrent[event.infoHash];

        const { torrents } = store.getState();
        if (torrents[event.infoHash]?.status !== "Copying") {
          delete this.preCopyStatusByTorrent[event.infoHash];
          return;
        }

        const restoreStatus = this.preCopyStatusByTorrent[event.infoHash] ?? "Idle";
        delete this.preCopyStatusByTorrent[event.infoHash];

        store.dispatch(
          updateTorrent({
            infoHash: event.infoHash,
            update: { status: restoreStatus },
          })
        );
      }
    );

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
      delete this.fileCopyInFlightByTorrent[infoHash];
      delete this.preCopyStatusByTorrent[infoHash];

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
      const existingStatus = store.getState().torrents[infoHash]?.status;

      const payload = {
        infoHash,
        update: {
          infoHash,
          name,
          sizeInBytes: totalSize,
          progress: 0,
          status: existingStatus ?? "Idle",
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

    this.connection.on(
      "FileSelectionChanged",
      (event: FileSelectionChangedEvent) => {
        const { torrentDetail } = store.getState();
        if (torrentDetail.selectedInfoHash === event.infoHash) {
          store.dispatch(
            updateDetailFiles({ selectedFileIds: event.selectedFileIds })
          );
        }
      }
    );
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
