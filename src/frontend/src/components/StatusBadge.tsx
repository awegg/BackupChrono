import React from 'react';
import {
  CheckCircle,
  XCircle,
  AlertTriangle,
  Loader2,
  MinusCircle,
  AlertCircle,
} from 'lucide-react';
import { BackupStatus } from '../data/mockBackupData';

interface StatusBadgeProps {
  status: BackupStatus;
  className?: string;
}

const statusConfig: Record<
  BackupStatus,
  {
    icon: React.ElementType;
    label: string;
    bgColor: string;
    textColor: string;
    iconColor: string;
  }
> = {
  Success: {
    icon: CheckCircle,
    label: 'Success',
    bgColor: 'bg-green-500/10 dark:bg-green-500/20',
    textColor: 'text-green-700 dark:text-green-400',
    iconColor: 'text-green-500',
  },
  Failed: {
    icon: XCircle,
    label: 'Failed',
    bgColor: 'bg-red-500/10 dark:bg-red-500/20',
    textColor: 'text-red-700 dark:text-red-400',
    iconColor: 'text-red-500',
  },
  Running: {
    icon: Loader2,
    label: 'Running',
    bgColor: 'bg-blue-500/10 dark:bg-blue-500/20',
    textColor: 'text-blue-700 dark:text-blue-400',
    iconColor: 'text-blue-500',
  },
  Warning: {
    icon: AlertTriangle,
    label: 'Warning',
    bgColor: 'bg-amber-500/10 dark:bg-amber-500/20',
    textColor: 'text-amber-700 dark:text-amber-400',
    iconColor: 'text-amber-500',
  },
  Disabled: {
    icon: MinusCircle,
    label: 'Disabled',
    bgColor: 'bg-slate-500/10 dark:bg-slate-500/20',
    textColor: 'text-slate-700 dark:text-slate-400',
    iconColor: 'text-slate-500',
  },
  Partial: {
    icon: AlertCircle,
    label: 'Partial',
    bgColor: 'bg-orange-500/10 dark:bg-orange-500/20',
    textColor: 'text-orange-700 dark:text-orange-400',
    iconColor: 'text-orange-500',
  },
};

const statusTooltips: Record<BackupStatus, string> = {
  Success: 'Backup completed successfully with no errors',
  Failed: 'Backup failed - check logs for details',
  Running: 'Backup is currently in progress',
  Warning: 'Backup completed with warnings - some files may have been skipped',
  Disabled: 'Backup schedule is disabled for this share',
  Partial: 'Backup partially succeeded - some files could not be backed up',
};

export const StatusBadge: React.FC<StatusBadgeProps> = ({
  status,
  className = '',
}) => {
  const config = statusConfig[status];
  const Icon = config.icon;
  const isRunning = status === 'Running';

  return (
    <div
      title={statusTooltips[status]}
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full ${config.bgColor} ${className}`}
    >
      <Icon
        className={`w-3.5 h-3.5 ${config.iconColor} ${isRunning ? 'animate-spin' : ''}`}
      />
      <span className={`text-xs font-medium ${config.textColor}`}>
        {config.label}
      </span>
    </div>
  );
};
