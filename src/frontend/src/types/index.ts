export enum ProtocolType {
  SMB = 'SMB',
  SSH = 'SSH',
  Rsync = 'Rsync',
}

export enum BackupJobStatus {
  Pending = 'Pending',
  Running = 'Running',
  Completed = 'Completed',
  Failed = 'Failed',
  Cancelled = 'Cancelled',
  PartiallyCompleted = 'PartiallyCompleted',
}

export enum BackupJobType {
  Scheduled = 'Scheduled',
  Manual = 'Manual',
  Retry = 'Retry',
}

export interface Schedule {
  cronExpression: string;
  timeWindowStart?: string;
  timeWindowEnd?: string;
}

export interface RetentionPolicy {
  keepLatest?: number;
  keepDaily?: number;
  keepWeekly?: number;
  keepMonthly?: number;
  keepYearly?: number;
}

export interface IncludeExcludeRules {
  includePatterns?: string[];
  excludePatterns?: string[];
}

export interface Device {
  id: string;
  name: string;
  protocol: ProtocolType;
  host: string;
  port: number;
  username: string;
  password?: string;
  wakeOnLanEnabled: boolean;
  wakeOnLanMacAddress?: string;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  createdAt: string;
  updatedAt: string;
}

export interface DeviceCreateDto {
  name: string;
  protocol: ProtocolType;
  host: string;
  port: number;
  username: string;
  password: string;
  wakeOnLanEnabled: boolean;
  wakeOnLanMacAddress?: string;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
}

export interface Share {
  id: string;
  deviceId: string;
  name: string;
  path: string;
  enabled: boolean;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  includeExcludeRules?: IncludeExcludeRules;
  createdAt: string;
  updatedAt: string;
}

export interface ShareCreateDto {
  name: string;
  path: string;
  enabled: boolean;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  includeExcludeRules?: IncludeExcludeRules;
}

export interface BackupJob {
  id: string;
  deviceId: string;
  deviceName?: string;
  shareId?: string;
  shareName?: string;
  type: BackupJobType;
  jobType?: string; // Backend returns this as well
  status: BackupJobStatus;
  startedAt?: string;
  completedAt?: string;
  backupId?: string;
  filesProcessed?: number;
  bytesTransferred?: number;
  errorMessage?: string;
  retryAttempt?: number;
  nextRetryAt?: string;
  commandLine?: string;
}

