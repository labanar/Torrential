import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import store from '../app/store';
import { addPeer, removePeer, updatePeer } from '../features/peersSlice';
import { PeerBitfieldReceivedEvent, PeerConnectedEvent, PeerDisconnectedEvent, PieceVerifiedEvent, TorrentRemovedEvent, TorrentStartedEvent, TorrentStatsEvent, TorrentStoppedEvent } from '@/api/events';
import { PeerSummary } from '@/types';
import { removeTorrent, updateTorrent } from '@/features/torrentsSlice';

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
        this.connection.on('PeerConnected', (event: PeerConnectedEvent) => {
            const summary: PeerSummary = {
                ...event,
                isSeed: false
            }
            store.dispatch(addPeer(summary));
        });

        this.connection.on('PeerDisconnected', (event: PeerDisconnectedEvent) => {
            const { infoHash, peerId } = event;
            const payload = {
                infoHash,
                peerId
            }
            store.dispatch(removePeer(payload))
        })

        this.connection.on('PeerBitfieldReceived', (event: PeerBitfieldReceivedEvent) => {
            store.dispatch(updatePeer({ infoHash: event.infoHash, peerId: event.peerId, update: { isSeed: event.hasAllPieces } }))
        })

        this.connection.on(`PieceVerified`, (event: PieceVerifiedEvent) => {
            const { infoHash, progress } = event;
            const payload = {
                infoHash,
                update: { progress: Number(progress.toFixed(3)) }
            }
            store.dispatch(updateTorrent(payload));
        });

        this.connection.on('TorrentStarted', (event: TorrentStartedEvent) => {
            const { infoHash } = event;
            const payload = {
                infoHash,
                update: { status: "Running" }
            }
            store.dispatch(updateTorrent(payload));
        });

        this.connection.on('TorrentStopped', (event: TorrentStoppedEvent) => {
            const { infoHash } = event;
            const payload = {
                infoHash,
                update: { status: "Stopped" }
            }
            store.dispatch(updateTorrent(payload));
        })

        this.connection.on('TorrentRemoved', (event: TorrentRemovedEvent) => {
            const { infoHash } = event;
            const payload = {
                infoHash
            }
            store.dispatch(removeTorrent(payload));
        })

        this.connection.on('TorrentStatsUpdated', (event: TorrentStatsEvent) => {
            const {infoHash, uploadRate, downloadRate} = event;
            const payload = {
                infoHash,
                update: {uploadRate, downloadRate}
            }
            store.dispatch(updateTorrent(payload));
        });
    }

    public async startConnection(): Promise<void> {
        try {
            await this.connection.start();
            console.log('SignalR connection successfully started.');
        } catch (error) {
            console.error('SignalR Connection Error:', error);
        }
    }

    public async stopConnection(): Promise<void> {
        await this.connection.stop();
        console.log('SignalR connection stopped.');
    }
}

export default SignalRService;