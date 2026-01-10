export interface DashboardSummaryDto {
  stats: DashboardStatsDto;
  devices: DeviceDashboardDto[];
}

export interface DashboardStatsDto {
  totalDevices: number;
  totalShares: number;
  totalStoredBytes: number;
  recentFailures: number;
  runningJobs: number;
  systemHealth: 'Healthy' | 'Warning' | 'Critical';
}

export interface DeviceDashboardDto {
  id: string;
  name: string;
  type: string;
  status: string;
  shares: ShareDashboardDto[];
}

export interface ShareDashboardDto {
  id: string;
  name: string;
  status: string; // "Success", "Failed", "Running", "Warning", "Disabled", "Pending"
  lastBackupTime?: string;
  nextBackupTime?: string;
  totalSize: number;
  fileCount: number;
  lastBackupId?: string;
  lastJobId?: string;
}
