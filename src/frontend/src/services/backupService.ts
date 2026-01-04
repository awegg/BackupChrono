import { apiClient } from './api';
import { FileEntry, Backup } from '../types';

export const backupService = {
  async getBackupsForShare(
    deviceId: string,
    shareId: string
  ): Promise<Backup[]> {
    const response = await apiClient.get<Backup[]>('/api/backups', {
      params: {
        deviceId,
        shareId
      },
      timeout: 15000 // 15 seconds for listing backups
    });
    return response.data;
  },
  async browseBackupFiles(
    backupId: string,
    deviceId: string,
    shareId: string,
    path: string = '/'
  ): Promise<FileEntry[]> {
    const response = await apiClient.get<FileEntry[]>(`/api/backups/${backupId}/files`, {
      params: {
        deviceId,
        shareId,
        path
      },
      timeout: 60000 // 60 seconds for browsing backup files (restic can be slow)
    });
    return response.data;
  },

  getDownloadUrl(
    backupId: string,
    deviceId: string,
    shareId: string,
    filePath: string
  ): string {
    const params = new URLSearchParams({
      deviceId,
      shareId,
      filePath
    });
    return `${apiClient.defaults.baseURL}/api/backups/${backupId}/download?${params}`;
  }
};
