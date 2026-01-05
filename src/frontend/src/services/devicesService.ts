import { apiClient } from './api';
import { Device, DeviceProtocol, DeviceStatus, Share } from '../types/devices';

interface DeviceDto {
  id: string;
  name: string;
  protocol: string;
  host: string;
  port?: number;
  username: string;
  wakeOnLanEnabled: boolean;
  wakeOnLanMacAddress?: string;
  createdAt: string;
  updatedAt: string;
}

interface DeviceDetailDto extends DeviceDto {
  shares: ShareDto[];
  lastBackup?: {
    timestamp: string;
    status: string;
  };
}

interface ShareDto {
  id: string;
  deviceId: string;
  name: string;
  path: string;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
}

interface DeviceCreateDto {
  name: string;
  protocol: string;
  host: string;
  port?: number;
  username: string;
  password: string;
  wakeOnLanEnabled?: boolean;
  wakeOnLanMacAddress?: string;
}

interface DeviceUpdateDto {
  name?: string;
  protocol?: string;
  host?: string;
  port?: number;
  username?: string;
  password?: string;
  wakeOnLanEnabled?: boolean;
  wakeOnLanMacAddress?: string;
}

interface ShareCreateDto {
  name: string;
  path: string;
  enabled?: boolean;
}

interface ShareUpdateDto {
  name?: string;
  path?: string;
  enabled?: boolean;
}

const mapProtocol = (protocol: string): DeviceProtocol => {
  switch (protocol.toUpperCase()) {
    case 'SMB':
      return DeviceProtocol.SMB;
    case 'SSH':
      return DeviceProtocol.SSH;
    case 'RSYNC':
      return DeviceProtocol.Rsync;
    default:
      return DeviceProtocol.SMB;
  }
};

const mapShare = (dto: ShareDto): Share => ({
  id: dto.id,
  name: dto.name,
  path: dto.path,
  enabled: dto.enabled,
  lastBackup: undefined, // TODO: Get actual backup timestamp from API
});

const mapDevice = (dto: DeviceDetailDto): Device => {
  // Determine device status based on last backup
  let status = DeviceStatus.Unknown;
  if (dto.lastBackup) {
    const lastBackupDate = new Date(dto.lastBackup.timestamp);
    const hoursSinceBackup = (Date.now() - lastBackupDate.getTime()) / (1000 * 60 * 60);
    
    // Consider active if backed up within last 24 hours
    if (hoursSinceBackup < 24) {
      status = DeviceStatus.Active;
    } else {
      status = DeviceStatus.Offline;
    }
  }

  return {
    id: dto.id,
    name: dto.name,
    host: dto.host,
    protocol: mapProtocol(dto.protocol),
    status,
    lastBackup: dto.lastBackup?.timestamp,
    shares: dto.shares.map(mapShare),
  };
};

export const devicesService = {
  async getDevices(): Promise<Device[]> {
    const response = await apiClient.get<DeviceDto[]>('/api/devices');
    
    // Fetch detailed data for each device (includes shares)
    const detailPromises = response.data.map(device =>
      apiClient.get<DeviceDetailDto>(`/api/devices/${device.id}`)
    );
    
    const detailResponses = await Promise.all(detailPromises);
    return detailResponses.map(res => mapDevice(res.data));
  },

  async getDevice(deviceId: string): Promise<Device> {
    const response = await apiClient.get<DeviceDetailDto>(`/api/devices/${deviceId}`);
    return mapDevice(response.data);
  },

  async createDevice(device: DeviceCreateDto): Promise<Device> {
    const response = await apiClient.post<DeviceDto>('/api/devices', device);
    // Fetch full details including shares
    const detailResponse = await apiClient.get<DeviceDetailDto>(`/api/devices/${response.data.id}`);
    return mapDevice(detailResponse.data);
  },

  async updateDevice(deviceId: string, updates: DeviceUpdateDto): Promise<Device> {
    const response = await apiClient.put<DeviceDto>(`/api/devices/${deviceId}`, updates);
    // Fetch full details including shares
    const detailResponse = await apiClient.get<DeviceDetailDto>(`/api/devices/${deviceId}`);
    return mapDevice(detailResponse.data);
  },

  async deleteDevice(deviceId: string): Promise<void> {
    await apiClient.delete(`/api/devices/${deviceId}`);
  },

  async testConnection(deviceId: string): Promise<{ success: boolean; message: string }> {
    const response = await apiClient.post<{ success: boolean; message: string }>(
      `/api/devices/${deviceId}/test-connection`
    );
    return response.data;
  },

  async createShare(deviceId: string, share: ShareCreateDto): Promise<Share> {
    const response = await apiClient.post<ShareDto>(`/api/devices/${deviceId}/shares`, share);
    return mapShare(response.data);
  },

  async updateShare(deviceId: string, shareId: string, updates: ShareUpdateDto): Promise<Share> {
    const response = await apiClient.put<ShareDto>(
      `/api/devices/${deviceId}/shares/${shareId}`,
      updates
    );
    return mapShare(response.data);
  },

  async deleteShare(deviceId: string, shareId: string): Promise<void> {
    await apiClient.delete(`/api/devices/${deviceId}/shares/${shareId}`);
  },

  async triggerBackup(deviceId: string): Promise<void> {
    await apiClient.post('/api/backup-jobs', { deviceId, shareId: null });
  },

  async triggerShareBackup(deviceId: string, shareId: string): Promise<void> {
    await apiClient.post('/api/backup-jobs', { deviceId, shareId });
  },
};
