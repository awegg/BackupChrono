import { Server, CheckCircle, AlertTriangle, FolderOpen, FileText } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

interface CompletedBackup {
  backupId: string;
  deviceId: string;
  shareId?: string;
  deviceName: string;
  path: string;
  status: 'Success' | 'Warning';
  duration: string;
  dataTransferred: string;
  completedAt: string;
}

interface RecentlyCompletedTableProps {
  backups: CompletedBackup[];
  onViewLogs?: (backupId: string) => void;
}
  const parseCompletedAt = (dateString: string): Date | null => {
    try {
      const date = new Date(dateString);
      return isNaN(date.getTime()) ? null : date;
    } catch {
      return null;
    }
  };
const getRelativeTime = (dateString: string): string => {
  try {
    // Try to parse as ISO date first
    let date = new Date(dateString);
    
    // If parsing failed or resulted in Invalid Date, try alternative formats
    if (isNaN(date.getTime())) {
      // Try parsing time-only format by appending today's date (local timezone)
      const today = new Date();
      const localDate = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
      date = new Date(`${localDate}T${dateString}`);
    }
    
    // If still invalid, just return the string
    if (isNaN(date.getTime())) {
      return dateString;
    }

    const now = new Date();
    const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    // Handle future times
    if (seconds < 0) return date.toLocaleDateString();

    if (seconds < 60) return 'just now';
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    return date.toLocaleDateString();
  } catch {
    return dateString;
  }
};

export function RecentlyCompletedTable({ backups, onViewLogs }: RecentlyCompletedTableProps) {
  const navigate = useNavigate();

  const handleViewLogs = (backup: CompletedBackup) => {
    const params = new URLSearchParams();
    if (backup.deviceId) params.append('deviceId', backup.deviceId);
    if (backup.shareId) params.append('shareId', backup.shareId);
    const query = params.toString();
    const url = query ? `/backups/${backup.backupId}/logs?${query}` : `/backups/${backup.backupId}/logs`;
    navigate(url);
    
    // Call optional callback
    if (onViewLogs) {
      onViewLogs(backup.backupId);
    }
  };

  if (backups.length === 0) {
    return (
      <div className="bg-card rounded-lg shadow-sm p-8 border border-border">
        <div className="text-center text-muted-foreground">
          No completed backups yet
        </div>
      </div>
    );
  }

  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      <table className="w-full">
        <thead className="bg-muted border-b border-border">
          <tr>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Device
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Path
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Status
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Duration
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Data Transferred
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Completed At
            </th>
            <th className="text-right px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Actions
            </th>
          </tr>
        </thead>
        <tbody>
          {backups.map((backup, index) => (
            <tr
              key={`${backup.deviceId}-${backup.path}-${index}`}
              className="border-b border-border last:border-0 hover:bg-muted/50 transition-colors"
            >
              <td className="px-4 py-3">
                <div className="flex items-center gap-2">
                  <Server className="w-4 h-4 text-muted-foreground" />
                  <span className="font-medium text-foreground">{backup.deviceName}</span>
                </div>
              </td>
              
              <td className="px-4 py-3">
                <span className="font-mono text-sm text-foreground">{backup.path}</span>
              </td>
              
              <td className="px-4 py-3">
                <span
                  className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-sm ${
                    backup.status === 'Success'
                      ? 'bg-status-success-bg text-status-success-fg'
                      : 'bg-status-warning-bg text-status-warning-fg'
                  }`}
                >
                  {backup.status === 'Success' ? (
                    <CheckCircle className="w-3.5 h-3.5" />
                  ) : (
                    <AlertTriangle className="w-3.5 h-3.5" />
                  )}
                  {backup.status}
                </span>
              </td>
              
              <td className="px-4 py-3">
                <span className="text-sm text-foreground">{backup.duration}</span>
              </td>
              
              <td className="px-4 py-3">
                <span className="text-sm font-medium text-foreground">{backup.dataTransferred}</span>
              </td>
              
              <td className="px-4 py-3">
                {(() => {
                  const d = parseCompletedAt(backup.completedAt);
                  const full = d ? d.toLocaleString() : backup.completedAt;
                  return (
                    <div className="group relative inline-block">
                      <span
                        className="text-sm text-muted-foreground cursor-help"
                        title={full}
                      >
                        {getRelativeTime(backup.completedAt)}
                      </span>
                      <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-2 py-1 bg-gray-900 text-white text-xs rounded opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap pointer-events-none z-10">
                        {full}
                      </div>
                    </div>
                  );
                })()}
              </td>
              
              <td className="px-4 py-3">
                <div className="flex items-center justify-end gap-2">
                  <button
                    onClick={() => handleViewLogs(backup)}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-foreground bg-card hover:bg-muted border border-border rounded-md transition-colors whitespace-nowrap"
                  >
                    <FileText className="w-3.5 h-3.5" />
                    View Details
                  </button>
                  <button
                    onClick={() => {
                      navigate(`/backups/${backup.backupId}/browse?deviceId=${backup.deviceId}&shareId=${backup.shareId}`);
                    }}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-foreground bg-card hover:bg-muted border border-border rounded-md transition-colors whitespace-nowrap"
                    disabled={!backup.backupId || !backup.shareId}
                  >
                    <FolderOpen className="w-3.5 h-3.5" />
                    Browse Backups
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
