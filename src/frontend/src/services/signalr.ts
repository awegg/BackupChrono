import * as signalR from '@microsoft/signalr';

export interface BackupProgress {
  jobId: string;
  deviceName: string;
  shareName: string;
  status: string;
  percentComplete?: number;
  filesProcessed?: number;
  totalFiles?: number;
  bytesProcessed?: number;
  totalBytes?: number;
  currentFile?: string;
  errorMessage?: string;
}

class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private listeners: Map<string, ((progress: BackupProgress) => void)[]> = new Map();

  async connect() {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/backup-progress')
      .withAutomaticReconnect()
      .build();

    this.connection.on('BackupProgress', (progress: BackupProgress) => {
      // Notify all listeners
      this.listeners.forEach(callbacks => {
        callbacks.forEach(callback => callback(progress));
      });
    });

    try {
      await this.connection.start();
      console.log('SignalR Connected');
    } catch (err) {
      console.error('SignalR Connection Error: ', err);
      // Will auto-reconnect
    }
  }

  async disconnect() {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  subscribe(id: string, callback: (progress: BackupProgress) => void) {
    if (!this.listeners.has(id)) {
      this.listeners.set(id, []);
    }
    this.listeners.get(id)!.push(callback);

    // Ensure connection is established
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      this.connect();
    }
  }

  unsubscribe(id: string) {
    this.listeners.delete(id);
  }
}

export const signalRService = new SignalRService();
