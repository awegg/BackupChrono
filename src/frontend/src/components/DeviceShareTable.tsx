import React from 'react';
import { useNavigate } from 'react-router-dom';
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
  onRowClick?: () => void;
  onLastBackupClick?: () => void;
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
  onRowClick,
  onLastBackupClick,
  isExpanded,
  onToggle,
}) => {
  const borderColor = statusBorderColors[status];
  const rowBg = isDevice
    ? 'bg-slate-100 dark:bg-slate-700/70'
    : 'bg-white dark:bg-slate-800';

  return (
    <tr
      className={`border-l-4 ${borderColor} ${rowBg} hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors group ${!isDevice && onRowClick ? 'cursor-pointer' : ''}`}
      onClick={(e) => {
        // Don't trigger row click if clicking on action buttons or toggle
        if (!isDevice && onRowClick && !(e.target as HTMLElement).closest('button')) {
          onRowClick();
        }
      }}
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
            className={`text-sm text-slate-600 dark:text-slate-400 font-mono ${!isDevice && onLastBackupClick && lastBackup ? 'hover:text-blue-600 dark:hover:text-blue-400 cursor-pointer underline decoration-dotted' : ''}`}
            title={getAbsoluteTimestamp(lastBackup)}
            onClick={(e) => {
              if (!isDevice && onLastBackupClick && lastBackup) {
                e.stopPropagation();
                onLastBackupClick();
              }
            }}
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
  const navigate = useNavigate();

  const handleBackupNow = (shareId: string) => {
    console.log('Backup now clicked for share:', shareId);
    // TODO: Implement backup trigger
  };

  const handleBrowse = (deviceId: string, shareId: string) => {
    console.log('Browse clicked for device:', deviceId, 'share:', shareId);
    // Navigate to browse page - need to get the latest backup first
    navigate(`/devices/${deviceId}`);
  };

  const handleViewLogs = (deviceId: string, shareId: string) => {
    console.log('View logs clicked for device:', deviceId, 'share:', shareId);
    // Navigate to backups list for this device/share
    navigate(`/devices/${deviceId}/backups?shareId=${shareId}`);
  };

  const handleShareRowClick = (deviceId: string, shareId: string) => {
    // Clicking on the share row goes to backups list
    navigate(`/devices/${deviceId}/backups?shareId=${shareId}`);
  };

  const handleLastBackupClick = async (deviceId: string, shareId: string) => {
    // Clicking on last backup timestamp browses the latest backup
    try {
      // Fetch backups for this device to get the latest one
      const response = await fetch(`http://localhost:5192/api/devices/${deviceId}/backups`);
      const backups = await response.json();
      
      // Filter by shareId if available and get the most recent
      const shareBackups = backups.filter((b: any) => !b.shareId || b.shareId === shareId);
      if (shareBackups.length > 0) {
        // Sort by timestamp descending and get the latest
        shareBackups.sort((a: any, b: any) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
        const latestBackup = shareBackups[0];
        
        // Navigate to browse the latest backup
        navigate(`/devices/${deviceId}/backups/${latestBackup.id}/browse?deviceId=${deviceId}&shareId=${shareId}`);
      } else {
        // No backups found, navigate to backups list instead
        navigate(`/devices/${deviceId}/backups?shareId=${shareId}`);
      }
    } catch (error) {
      console.error('Error fetching latest backup:', error);
      // Fall back to backups list
      navigate(`/devices/${deviceId}/backups?shareId=${shareId}`);
    }
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
                    onBrowse={() => handleBrowse(device.id, share.id)}
                    onViewLogs={() => handleViewLogs(device.id, share.id)}
                    onRowClick={() => handleShareRowClick(device.id, share.id)}
                    onLastBackupClick={() => handleLastBackupClick(device.id, share.id)}
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
