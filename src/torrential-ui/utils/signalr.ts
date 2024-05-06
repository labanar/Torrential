import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';

export const createSignalRConnection = async (hubUrl: string): Promise<HubConnection> => {
    const connection = new HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    try {
        await connection.start();
        console.log('SignalR Connected.');
    } catch (err) {
        console.error('Error while starting SignalR connection: ', err);
    }

    return connection;
};
