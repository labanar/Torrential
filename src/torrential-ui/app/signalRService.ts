import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import store from '../app/store';
import { addPeer, updatePeer } from '../features/peersSlice';
import { PeerBitfieldReceivedEvent, PeerConnectedEvent, PieceVerifiedEvent } from '@/api/events';
import { PeerSummary } from '@/types';
import { updateTorrent } from '@/features/torrentsSlice';

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
            const summary : PeerSummary = {
              ...event,
              isSeed: false
            }
            store.dispatch(addPeer(summary));
        });

        this.connection.on('PeerBitfieldReceived', (event: PeerBitfieldReceivedEvent) => {
            store.dispatch(updatePeer({infoHash: event.infoHash, peerId: event.peerId, update: {isSeed: event.hasAllPieces}}))
        })

        this.connection.on(`PieceVerified`, (event: PieceVerifiedEvent) => {
            const {infoHash, progress} = event;
            const payload = {
                infoHash,
                update: {progress: Number(progress.toFixed(3))}
            }

            store.dispatch(updateTorrent(payload));
        })

        // Add more event handlers as necessary
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