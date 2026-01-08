export type BackupStatus = 'Success' | 'Failed' | 'Running' | 'Warning' | 'Disabled' | 'Partial';

export interface ShareSummary {
  id: string;
  name: string;
  path: string;
  lastBackup: Date | null;
  status: BackupStatus;
  sizeGB: number;
  fileCount: number;
}

export interface DeviceSummary {
  id: string;
  name: string;
  status: BackupStatus;
  sizeGB: number;
  fileCount: number;
  shares: ShareSummary[];
}

export interface DashboardSummary {
  devicesNeedingAttention: number;
  totalProtectedDataTB: number;
  recentFailures: number;
  devices: DeviceSummary[];
}
