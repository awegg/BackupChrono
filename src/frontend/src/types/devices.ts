export enum DeviceProtocol {
  SMB = 'SMB',
  SSH = 'SSH',
  Rsync = 'Rsync',
}

export enum DeviceStatus {
  Active = 'Active',
  Offline = 'Offline',
  Unknown = 'Unknown',
}

import { Schedule, RetentionPolicy, IncludeExcludeRules } from './index';

export interface Share {
  id: string;
  name: string;
  path: string;
  enabled: boolean;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  includeExcludeRules?: IncludeExcludeRules;
  lastBackup?: string;
}

export interface Device {
  id: string;
  name: string;
  host: string;
  protocol: DeviceProtocol;
  status: DeviceStatus;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  includeExcludeRules?: IncludeExcludeRules;
  lastBackup?: string;
  shares: Share[];
}
