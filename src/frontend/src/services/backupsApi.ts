import { apiClient } from './api';

export interface BackupDetailApi {
  id: string;
  deviceId: string;
  shareId?: string | null;
  deviceName: string;
  shareName?: string | null;
  timestamp: string;
  status: string;
  sharesPaths: Record<string, string>;
  fileStats: {
    new: number;
    changed: number;
    unmodified: number;
  };
  dataStats: {
    added: number;
    processed: number;
  };
  duration: string;
  errorMessage?: string | null;
  createdByJobId?: string | null;
  directoryStats: {
    new: number;
    changed: number;
    unmodified: number;
  };
  snapshotInfo: {
    snapshotId: string;
    parentSnapshot: string | null;
    exitCode: number;
  };
  deduplicationInfo: {
    dataBlobs: number;
    treeBlobs: number;
    ratio: string;
    spaceSaved: string;
  };
  shares: Array<{
    name: string;
    path: string;
  }>;
}

export interface BackupLogsApi {
  warnings: string[];
  errors: string[];
  progressLog: Array<{
    timestamp: string;
    message: string;
    percentDone?: number;
    currentFiles?: string[] | null;
    filesDone?: number;
    bytesDone?: number;
  }>;
}

export async function getBackupDetail(backupId: string, deviceId?: string, shareId?: string): Promise<BackupDetailApi> {
  const params = new URLSearchParams();
  if (deviceId) params.append('deviceId', deviceId);
  if (shareId) params.append('shareId', shareId);
  const query = params.toString();
  const url = query ? `/api/backups/${backupId}?${query}` : `/api/backups/${backupId}`;
  const response = await apiClient.get<BackupDetailApi>(url);
  return response.data;
}

export async function getBackupLogs(backupId: string, deviceId?: string, shareId?: string): Promise<BackupLogsApi> {
  const params = new URLSearchParams();
  if (deviceId) params.append('deviceId', deviceId);
  if (shareId) params.append('shareId', shareId);
  const query = params.toString();
  const url = query ? `/api/backups/${backupId}/logs?${query}` : `/api/backups/${backupId}/logs`;
  const response = await apiClient.get<BackupLogsApi>(url);
  return response.data;
}
