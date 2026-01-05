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
                <span className="text-sm text-muted-foreground">{backup.completedAt}</span>
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
                      console.log('Browse backups clicked:', { backupId: backup.backupId });
                      navigate(`/backups/${backup.backupId}/browse`);
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
