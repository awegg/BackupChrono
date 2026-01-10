import { apiClient } from './api';
import { BackupJob, Backup, BackupStatus, BackupJobStatus, DashboardSummaryDto } from '../types';

export interface DashboardStats {
  activeJobs: number;
  queuedJobs: number;
  completedJobs: number;
  failedJobs: number;
  avgSpeed: string;
  dataToday: string;
}

export const dashboardService = {
  async getSummary(): Promise<DashboardSummaryDto> {
    const response = await apiClient.get<DashboardSummaryDto>('/api/dashboard/summary');
    return response.data;
  },

  async getStats(): Promise<DashboardStats> {
    // TODO: Replace with actual API endpoint when available
    // For now, calculate from available endpoints
    try {
      const jobs = await this.getActiveJobs();
      const recentBackups = await this.getRecentBackups();

      const activeJobs = jobs.filter(j => j.status === BackupJobStatus.Running).length;
      const queuedJobs = jobs.filter(j => j.status === BackupJobStatus.Pending).length;
      const completedToday = recentBackups.filter(b =>
        b.status === BackupStatus.Success &&
        new Date(b.timestamp).toDateString() === new Date().toDateString()
      ).length;
      const failedToday = recentBackups.filter(b =>
        b.status === BackupStatus.Failed &&
        new Date(b.timestamp).toDateString() === new Date().toDateString()
      ).length;

      // Calculate total data transferred today
      const totalBytesToday = recentBackups
        .filter(b => new Date(b.timestamp).toDateString() === new Date().toDateString())
        .reduce((sum, b) => sum + (b.dataAdded || 0), 0);

      // Calculate average speed from active jobs
      const runningJobs = jobs.filter(j => j.status === 'Running' && j.bytesTransferred && j.startedAt);
      const avgSpeedMBs = runningJobs
        .map(j => {
          const elapsed = (new Date().getTime() - new Date(j.startedAt!).getTime()) / 1000;
          return elapsed > 0 ? (j.bytesTransferred! / elapsed / 1024 / 1024) : 0;
        })
        .reduce((sum, speed) => sum + speed, 0) / Math.max(runningJobs.length, 1);

      return {
        activeJobs,
        queuedJobs,
        completedJobs: completedToday,
        failedJobs: failedToday,
        avgSpeed: avgSpeedMBs > 0 ? `${Math.round(avgSpeedMBs)} MB/s` : '0 MB/s',
        dataToday: this.formatBytes(totalBytesToday),
      };
    } catch (error) {
      console.error('Failed to fetch dashboard stats:', error);
      return {
        activeJobs: 0,
        queuedJobs: 0,
        completedJobs: 0,
        failedJobs: 0,
        avgSpeed: '0 MB/s',
        dataToday: '0 B',
      };
    }
  },

  async getActiveJobs(): Promise<BackupJob[]> {
    const response = await apiClient.get<BackupJob[]>('/api/backup-jobs');
    return response.data.filter(job =>
      job.status === 'Running' || job.status === 'Pending'
    );
  },

  async getRecentBackups(limit: number = 10): Promise<Backup[]> {
    // Get recently completed backup jobs (not live restic snapshots)
    // Fetch more jobs to ensure we get enough completed ones after filtering
    const response = await apiClient.get<BackupJob[]>('/api/backup-jobs', {
      params: { limit: 100 } // Fetch more jobs to ensure we get completed ones
    });

    // Filter for completed jobs (Success, Failed, Cancelled - not Running/Pending)
    const completedJobs = response.data
      .filter(job =>
        job.status !== 'Running' &&
        job.status !== 'Pending'
      )
      .sort((a, b) => {
        const aTime = new Date(a.completedAt || a.startedAt || 0).getTime();
        const bTime = new Date(b.completedAt || b.startedAt || 0).getTime();
        return bTime - aTime;
      })
      .slice(0, limit);

    // Map BackupJob to Backup format for the UI
    return completedJobs.map(job => {
      // Map BackupJobStatus to BackupStatus
      let backupStatus: BackupStatus;
      if (job.status === 'Completed') {
        backupStatus = BackupStatus.Success;
      } else if (job.status === 'PartiallyCompleted') {
        backupStatus = BackupStatus.Partial;
      } else {
        backupStatus = BackupStatus.Failed; // Failed or Cancelled
      }

      return {
        id: job.backupId || job.id,
        deviceId: job.deviceId,
        shareId: job.shareId || undefined,
        deviceName: job.deviceName || 'Unknown Device',
        shareName: job.shareName || undefined,
        timestamp: job.completedAt || job.startedAt || new Date().toISOString(),
        status: backupStatus,
        sharesPaths: {},
        filesNew: 0,
        filesChanged: 0,
        filesUnmodified: job.filesProcessed || 0,
        dataAdded: job.bytesTransferred || 0,
        dataProcessed: job.bytesTransferred || 0,
        duration: job.completedAt && job.startedAt
          ? `${Math.floor((new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()) / 1000)}s`
          : '0s',
        errorMessage: job.errorMessage
      };
    });
  },

  formatBytes(bytes: number): string {
    if (!isFinite(bytes) || bytes <= 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.max(0, Math.min(Math.floor(Math.log(bytes) / Math.log(k)), sizes.length - 1));
    return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
  },

  formatDuration(startedAt: string, completedAt?: string): string {
    const start = new Date(startedAt).getTime();
    const end = completedAt ? new Date(completedAt).getTime() : new Date().getTime();
    const durationMs = end - start;

    const seconds = Math.floor(durationMs / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
      return `${hours}h ${minutes % 60}m ${seconds % 60}s`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    } else {
      return `${seconds}s`;
    }
  },

  calculateETA(bytesTransferred: number, totalBytes: number, speed: number): string {
    if (speed === 0 || totalBytes === 0) return 'Unknown';

    const remainingBytes = totalBytes - bytesTransferred;
    const secondsRemaining = remainingBytes / (speed * 1024 * 1024); // speed is in MB/s

    const minutes = Math.floor(secondsRemaining / 60);
    const seconds = Math.floor(secondsRemaining % 60);

    if (minutes > 0) {
      return `${minutes}m ${seconds}s`;
    } else {
      return `${seconds}s`;
    }
  },

  calculateSpeed(bytesTransferred: number, startedAt: string): number {
    const elapsed = (new Date().getTime() - new Date(startedAt).getTime()) / 1000;
    if (elapsed === 0) return 0;
    return bytesTransferred / elapsed / 1024 / 1024; // MB/s
  },
};
