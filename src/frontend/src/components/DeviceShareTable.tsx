import React from 'react';
import { Server, Folder, Play, FolderOpen, FileText, ChevronUp, ChevronDown, Clock } from 'lucide-react';
import { DeviceSummary, BackupStatus } from '../data/mockBackupData';
import { StatusBadge } from './StatusBadge';
import {
  formatTimestampSmart as formatTimestamp,
  getAbsoluteTimestamp,
  formatSize,
  formatFileCount,
} from '../utils/timeFormat';

type SortField = 'name' | 'lastBackup' | 'status' | 'size' | 'files';
type SortDirection = 'asc' | 'desc';

interface DeviceShareTableProps {
  devices: DeviceSummary[];
  sortField?: SortField;
  sortDirection?: SortDirection;
  onSort?: (field: SortField) => void;
  expandedDevices?: Set<string>;
  onToggleDevice?: (deviceId: string) => void;
}

const statusBorderColors: Record<BackupStatus, string> = {
  Success: 'border-l-green-500',
  Failed: 'border-l-red-500',
  Running: 'border-l-blue-500',
  Warning: 'border-l-amber-500',
  Disabled: 'border-l-slate-500',
  Partial: 'border-l-orange-500',
};

interface ActionButtonProps {
  icon: React.ElementType;
  label: string;
  onClick: () => void;
  variant?: 'primary' | 'secondary';
}

const ActionButton: React.FC<ActionButtonProps> = ({
  icon: Icon,
  label,
  onClick,
  variant = 'secondary',
}) => {
  const colorClass =
    variant === 'primary'
      ? 'text-blue-600 dark:text-blue-400 hover:bg-blue-500/10 dark:hover:bg-blue-500/20'
      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-500/10 dark:hover:bg-slate-500/20';

  return (
    <button
      onClick={onClick}
      className={`p-2 rounded-lg transition-colors ${colorClass}`}
      title={label}
      aria-label={label}
    >
      <Icon className="w-4 h-4" />
    </button>
  );
};

interface TableRowProps {
  isDevice: boolean;
  name: string;
  path?: string;
  lastBackup: Date | null;
  status: BackupStatus;
  sizeGB: number;
  fileCount: number;
  onBackupNow: () => void;
  onBrowse: () => void;
  onViewLogs: () => void;
  isExpanded?: boolean;
  onToggle?: () => void;
}

const TableRow: React.FC<TableRowProps> = ({
  isDevice,
  name,
  path,
  lastBackup,
  status,
  sizeGB,
  fileCount,
  onBackupNow,
  onBrowse,
  onViewLogs,
  isExpanded,
  onToggle,
}) => {
  const borderColor = statusBorderColors[status];
  const rowBg = isDevice
    ? 'bg-slate-100 dark:bg-slate-700/70'
    : 'bg-white dark:bg-slate-800';

  return (
    <tr
      className={`border-l-4 ${borderColor} ${rowBg} hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors group`}
    >
      <td className="px-6 py-3">
        <div className={`flex items-center gap-2 ${!isDevice ? 'pl-8' : ''}`}>
          {isDevice && (
            <button
              onClick={onToggle}
              className="p-0.5 hover:bg-slate-200 dark:hover:bg-slate-600 rounded transition-colors"
              aria-label={isExpanded ? 'Collapse device' : 'Expand device'}
            >
              {isExpanded ? (
                <ChevronDown className="w-4 h-4 text-slate-600 dark:text-slate-400" />
              ) : (
                <ChevronUp className="w-4 h-4 text-slate-600 dark:text-slate-400" />
              )}
            </button>
          )}
          {isDevice ? (
            <Server className="w-4 h-4 text-slate-600 dark:text-slate-400" />
          ) : (
            <Folder className="w-4 h-4 text-slate-600 dark:text-slate-400" />
          )}
          <span
            className={`${
              isDevice
                ? 'font-bold text-base text-slate-900 dark:text-white'
                : 'text-sm text-slate-700 dark:text-slate-300'
            }`}
          >
            {isDevice ? name : path || name}
          </span>
        </div>
      </td>
      <td className="px-6 py-3">
        <div className="flex items-center gap-2">
          <span
            className="text-sm text-slate-600 dark:text-slate-400 font-mono"
            title={getAbsoluteTimestamp(lastBackup)}
          >
            {isDevice ? '—' : formatTimestamp(lastBackup)}
          </span>
          {!isDevice && (() => {
            // Check if backup is stale (>2 days or null)
            const isStale = !lastBackup || (() => {
              const twoDaysAgo = new Date();
              twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);
              return lastBackup < twoDaysAgo;
            })();
            
            if (isStale) {
              return (
                <Clock 
                  className="w-4 h-4 text-orange-500 dark:text-orange-400" 
                  title={lastBackup ? "Backup is older than 2 days" : "Never backed up"}
                />
              );
            }
            return null;
          })()}
        </div>
      </td>
      <td className="px-6 py-3">
        <StatusBadge status={status} />
      </td>
      <td className="px-6 py-3">
        <span className="text-sm text-slate-700 dark:text-slate-300 font-mono">
          {formatSize(sizeGB)}
        </span>
      </td>
      <td className="px-6 py-3">
        <span className="text-sm text-slate-700 dark:text-slate-300 font-mono">
          {formatFileCount(fileCount)}
        </span>
      </td>
      <td className="px-6 py-3">
        {!isDevice && (
          <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
            <ActionButton
              icon={Play}
              label="Backup Now"
              onClick={onBackupNow}
              variant="primary"
            />
            <ActionButton
              icon={FolderOpen}
              label="Browse Latest"
              onClick={onBrowse}
            />
            <ActionButton
              icon={FileText}
              label="View Logs"
              onClick={onViewLogs}
            />
          </div>
        )}
      </td>
    </tr>
  );
};

export const DeviceShareTable: React.FC<DeviceShareTableProps> = ({
  devices,
  sortField,
  sortDirection,
  onSort,
  expandedDevices = new Set(),
  onToggleDevice,
}) => {
  const handleBackupNow = (shareId: string) => {
    console.log('Backup now clicked for share:', shareId);
    // TODO: Implement backup trigger
  };

  const handleBrowse = (shareId: string) => {
    console.log('Browse clicked for share:', shareId);
    // TODO: Navigate to backup browser
  };

  const handleViewLogs = (shareId: string) => {
    console.log('View logs clicked for share:', shareId);
    // TODO: Open logs dialog
  };

  const SortableHeader: React.FC<{ field: SortField; children: React.ReactNode }> = ({ field, children }) => {
    const isActive = sortField === field;
    const showArrow = isActive;

    return (
      <th
        className="px-6 py-3 text-left text-xs font-medium text-slate-700 dark:text-slate-300 uppercase tracking-wider cursor-pointer hover:bg-slate-200 dark:hover:bg-slate-600 transition-colors select-none"
        onClick={() => onSort?.(field)}
      >
        <div className="flex items-center gap-1">
          {children}
          {showArrow && (
            sortDirection === 'asc' ? (
              <ChevronUp className="w-3 h-3" />
            ) : (
              <ChevronDown className="w-3 h-3" />
            )
          )}
        </div>
      </th>
    );
  };

  return (
    <div className="bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 overflow-hidden">
      <table className="w-full">
        <thead>
          <tr className="bg-slate-100 dark:bg-slate-700 border-b border-slate-200 dark:border-slate-600">
            <SortableHeader field="name">Device/Share</SortableHeader>
            <SortableHeader field="lastBackup">Last Backup</SortableHeader>
            <SortableHeader field="status">Status</SortableHeader>
            <SortableHeader field="size">Size</SortableHeader>
            <SortableHeader field="files">Files</SortableHeader>
            <th className="px-6 py-3 text-left text-xs font-medium text-slate-700 dark:text-slate-300 uppercase tracking-wider">
              Actions
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-200 dark:divide-slate-700">
          {devices.map((device) => {
            const isExpanded = expandedDevices.has(device.id);
            return (
              <React.Fragment key={device.id}>
                <TableRow
                  isDevice={true}
                  name={device.name}
                  lastBackup={null}
                  status={device.status}
                  sizeGB={device.sizeGB}
                  fileCount={device.fileCount}
                  onBackupNow={() => {}}
                  onBrowse={() => {}}
                  onViewLogs={() => {}}
                  isExpanded={isExpanded}
                  onToggle={() => onToggleDevice?.(device.id)}
                />
                {isExpanded && device.shares.map((share) => (
                  <TableRow
                    key={share.id}
                    isDevice={false}
                    name={share.name}
                    path={share.path}
                    lastBackup={share.lastBackup}
                    status={share.status}
                    sizeGB={share.sizeGB}
                    fileCount={share.fileCount}
                    onBackupNow={() => handleBackupNow(share.id)}
                    onBrowse={() => handleBrowse(share.id)}
                    onViewLogs={() => handleViewLogs(share.id)}
                  />
                ))}
              </React.Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};
