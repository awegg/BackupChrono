// Mock data for Backup Overview dashboard

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

export const mockDashboardData: DashboardSummary = {
  devicesNeedingAttention: 1,
  totalProtectedDataTB: 1.6,
  recentFailures: 0,
  devices: [
    {
      id: 'nas-server',
      name: 'nas-server',
      status: 'Success',
      sizeGB: 476.8,
      fileCount: 245678,
      shares: [
        {
          id: 'nas-documents',
          name: 'documents',
          path: '/mnt/documents',
          lastBackup: new Date(Date.now() - 2 * 60 * 60 * 1000), // 2 hours ago
          status: 'Success',
          sizeGB: 122.9,
          fileCount: 98234,
        },
        {
          id: 'nas-photos',
          name: 'photos',
          path: '/mnt/photos',
          lastBackup: new Date(Date.now() - 15 * 60 * 1000), // 15 mins ago
          status: 'Running',
          sizeGB: 238.4,
          fileCount: 125678,
        },
        {
          id: 'nas-videos',
          name: 'videos',
          path: '/mnt/videos',
          lastBackup: new Date('2026-01-03T20:57:00'),
          status: 'Failed',
          sizeGB: 115.5,
          fileCount: 21766,
        },
      ],
    },
    {
      id: 'fileserver',
      name: 'fileserver',
      status: 'Warning',
      sizeGB: 830.7,
      fileCount: 567890,
      shares: [
        {
          id: 'fileserver-data',
          name: 'data',
          path: '/data',
          lastBackup: new Date('2026-01-03T21:01:00'),
          status: 'Warning',
          sizeGB: 631.4,
          fileCount: 456789,
        },
        {
          id: 'fileserver-backup',
          name: 'backup',
          path: '/backup',
          lastBackup: null,
          status: 'Disabled',
          sizeGB: 199.3,
          fileCount: 111101,
        },
      ],
    },
    {
      id: 'workstation',
      name: 'workstation',
      status: 'Success',
      sizeGB: 217.9,
      fileCount: 89345,
      shares: [
        {
          id: 'workstation-home',
          name: 'home',
          path: '/home/user',
          lastBackup: new Date(Date.now() - 24 * 60 * 60 * 1000), // 1 day ago
          status: 'Partial',
          sizeGB: 217.9,
          fileCount: 89345,
        },
      ],
    },
    {
      id: 'prod-server-01',
      name: 'prod-server-01',
      status: 'Success',
      sizeGB: 145.3,
      fileCount: 234567,
      shares: [
        {
          id: 'prod-html',
          name: 'html',
          path: '/var/www/html',
          lastBackup: new Date(Date.now() - 1 * 60 * 60 * 1000), // 1 hour ago
          status: 'Success',
          sizeGB: 145.3,
          fileCount: 234567,
        },
      ],
    },
  ],
};
