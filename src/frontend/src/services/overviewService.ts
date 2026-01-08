import { apiClient } from './api';
import type { BackupStatus, ShareSummary, DeviceSummary, DashboardSummary } from '../types/overview';

// Re-export types for consumers of this service
export type { BackupStatus, ShareSummary, DeviceSummary, DashboardSummary };

// API response types matching backend DTOs
interface ShareOverviewDto {
  id: string;
  name: string;
  path: string;
  lastBackupTimestamp: string | null;
  status: string;
  sizeGB: number;
  fileCount: number;
  isStale: boolean;
}

interface DeviceOverviewDto {
  id: string;
  name: string;
  status: string;
  sizeGB: number;
  fileCount: number;
  shares: ShareOverviewDto[];
}

interface BackupOverviewDto {
  devicesNeedingAttention: number;
  totalProtectedDataTB: number;
  recentFailures: number;
  devices: DeviceOverviewDto[];
}

/**
 * Maps backend status string to frontend BackupStatus type.
 */
function mapBackupStatus(status: string): BackupStatus {
  switch (status) {
    case 'Success':
      return 'Success';
    case 'Failed':
      return 'Failed';
    case 'Running':
      return 'Running';
    case 'Warning':
      return 'Warning';
    case 'Disabled':
      return 'Disabled';
    case 'Partial':
      return 'Partial';
    default:
      return 'Warning';
  }
}

/**
 * Maps backend ShareOverviewDto to frontend ShareSummary.
 */
function mapShareSummary(dto: ShareOverviewDto): ShareSummary {
  return {
    id: dto.id,
    name: dto.name,
    path: dto.path,
    lastBackup: dto.lastBackupTimestamp ? new Date(dto.lastBackupTimestamp) : null,
    status: mapBackupStatus(dto.status),
    sizeGB: dto.sizeGB,
    fileCount: dto.fileCount,
  };
}

/**
 * Maps backend DeviceOverviewDto to frontend DeviceSummary.
 */
function mapDeviceSummary(dto: DeviceOverviewDto): DeviceSummary {
  return {
    id: dto.id,
    name: dto.name,
    status: mapBackupStatus(dto.status),
    sizeGB: dto.sizeGB,
    fileCount: dto.fileCount,
    shares: dto.shares.map(mapShareSummary),
  };
}

/**
 * Service for fetching backup overview data.
 */
export const overviewService = {
  /**
   * Fetches complete backup overview dashboard data from the API.
   * Includes all devices, shares, and summary statistics.
   */
  async getOverviewData(): Promise<DashboardSummary> {
    const response = await apiClient.get<BackupOverviewDto>('/api/overview');
    
    // Map backend DTOs to frontend types
    return {
      devicesNeedingAttention: response.data.devicesNeedingAttention,
      totalProtectedDataTB: response.data.totalProtectedDataTB,
      recentFailures: response.data.recentFailures,
      devices: response.data.devices.map(mapDeviceSummary),
    };
  },
};
