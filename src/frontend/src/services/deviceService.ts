import { apiClient } from './api';
// import { Device, DeviceCreateDto, Share, ShareCreateDto, BackupJob } from '../types';
import { Device, DeviceCreateDto, Share, ShareCreateDto } from '../types';
import type { BackupJob } from '../types';

export const deviceService = {
  listDevices: async (): Promise<Device[]> => {
    const response = await apiClient.get('/api/devices');
    return response.data;
  },

  getDevice: async (id: string): Promise<Device> => {
    const response = await apiClient.get(`/api/devices/${id}`);
    return response.data;
  },

  createDevice: async (device: DeviceCreateDto): Promise<Device> => {
    const response = await apiClient.post('/api/devices', device);
    return response.data;
  },

  updateDevice: async (id: string, device: Partial<Device>): Promise<Device> => {
    const response = await apiClient.put(`/api/devices/${id}`, device);
    return response.data;
  },

  deleteDevice: async (id: string): Promise<void> => {
    await apiClient.delete(`/api/devices/${id}`);
  },

  testConnection: async (id: string): Promise<boolean> => {
    const response = await apiClient.post(`/api/devices/${id}/test-connection`);
    return response.data;
  },

  wake: async (id: string): Promise<void> => {
    await apiClient.post(`/api/devices/${id}/wake`);
  },
};

export const shareService = {
  listShares: async (deviceId: string): Promise<Share[]> => {
    const response = await apiClient.get(`/api/devices/${deviceId}/shares`);
    return response.data;
  },

  getShare: async (deviceId: string, shareId: string): Promise<Share> => {
    const response = await apiClient.get(`/api/devices/${deviceId}/shares/${shareId}`);
    return response.data;
  },

  createShare: async (deviceId: string, share: ShareCreateDto): Promise<Share> => {
    const response = await apiClient.post(`/api/devices/${deviceId}/shares`, share);
    return response.data;
  },

  updateShare: async (deviceId: string, shareId: string, share: Partial<Share>): Promise<Share> => {
    const response = await apiClient.put(`/api/devices/${deviceId}/shares/${shareId}`, share);
    return response.data;
  },

  deleteShare: async (deviceId: string, shareId: string): Promise<void> => {
    await apiClient.delete(`/api/devices/${deviceId}/shares/${shareId}`);
  },

  setEnabled: async (deviceId: string, shareId: string, enabled: boolean): Promise<void> => {
    await apiClient.patch(`/api/devices/${deviceId}/shares/${shareId}/enabled`, { enabled });
  },
};

export const backupService = {
  listJobs: async (): Promise<BackupJob[]> => {
    const response = await apiClient.get('/api/backup-jobs');
    return response.data;
  },

  getJob: async (id: string): Promise<BackupJob> => {
    const response = await apiClient.get(`/api/backup-jobs/${id}`);
    return response.data;
  },

  triggerDeviceBackup: async (deviceId: string): Promise<BackupJob> => {
    const response = await apiClient.post('/api/backup-jobs', { deviceId });
    return response.data;
  },

  triggerShareBackup: async (deviceId: string, shareId: string): Promise<BackupJob> => {
    const response = await apiClient.post('/api/backup-jobs', { deviceId, shareId });
    return response.data;
  },

  deleteJob: async (id: string): Promise<void> => {
    await apiClient.delete(`/api/backup-jobs/${id}`);
  },

  cancelJob: async (id: string): Promise<void> => {
    await apiClient.post(`/api/backup-jobs/${id}/cancel`);
  },
};
