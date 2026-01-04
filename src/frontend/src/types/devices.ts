export enum DeviceProtocol {
  SMB = 'SMB',
  SSH = 'SSH',
  Rsync = 'Rsync',
}

export enum DeviceStatus {
  Active = 'Active',
  Offline = 'Offline',
}

export interface Share {
  id: string;
  name: string;
  path: string;
  enabled: boolean;
  lastBackup: string;
}

export interface Device {
  id: string;
  name: string;
  host: string;
  protocol: DeviceProtocol;
  status: DeviceStatus;
  lastBackup: string;
  shares: Share[];
}
