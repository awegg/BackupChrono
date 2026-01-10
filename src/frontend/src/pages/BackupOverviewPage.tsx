import { useState, useEffect } from 'react';
import {
  Server,
  HardDrive,
  Activity,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Clock,
  Play,
  FileText,
  FolderOpen,
  StopCircle,
  RefreshCw,
  Database
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { dashboardService } from '../services/dashboardService';
import { backupService } from '../services/deviceService';
import { DashboardSummaryDto } from '../types';

export function BackupOverviewPage() {
  const [data, setData] = useState<DashboardSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [cancellingJobId, setCancellingJobId] = useState<string | null>(null);

  const loadData = async () => {
    try {
      setError(null);
      const summary = await dashboardService.getSummary();
      setData(summary);
    } catch (err) {
      setError('Failed to load dashboard data');
      console.error(err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadData();
    // Refresh every 10 seconds
    const interval = setInterval(loadData, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleRefresh = () => {
    setRefreshing(true);
    loadData();
  };

  const handleCancelJob = async (jobId: string) => {
    if (!confirm('Are you sure you want to cancel this running job?')) return;

    try {
      setCancellingJobId(jobId);
      await backupService.cancelJob(jobId);
      // Wait a bit then refresh
      setTimeout(handleRefresh, 1000);
    } catch (err) {
      alert('Failed to cancel job');
      console.error(err);
    } finally {
      setCancellingJobId(null);
    }
  };

  const handleTriggerBackup = async (deviceId: string, shareId: string) => {
    try {
      await backupService.triggerShareBackup(deviceId, shareId);
      // Wait a bit then refresh
      setTimeout(handleRefresh, 1000);
    } catch (err) {
      alert('Failed to trigger backup');
      console.error(err);
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = diffMs / (1000 * 60 * 60);

    // Handle future dates (negative diffMs)
    if (diffMs < 0) {
      const absDiffMs = Math.abs(diffMs);
      const absDiffHours = Math.abs(diffHours);
      
      if (absDiffHours < 24) {
        if (absDiffHours < 1) return 'in ' + Math.round(absDiffMs / (1000 * 60)) + ' mins';
        return 'in ' + Math.round(absDiffHours) + ' hours';
      }
      return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    // If less than 24h ago, show relative time
    if (diffHours < 24) {
      if (diffHours < 1) return Math.round(diffMs / (1000 * 60)) + ' mins ago';
      return Math.round(diffHours) + ' hours ago';
    }
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  if (loading && !data) {
    return (
      <div className="flex justify-center items-center h-screen">
        <RefreshCw className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-foreground">Backup Overview</h1>
          <p className="text-muted-foreground">System status and backup summary</p>
        </div>
        <button
          onClick={handleRefresh}
          className="p-2 hover:bg-accent rounded-full transition-colors"
          disabled={refreshing}
        >
          <RefreshCw className={`w-5 h-5 ${refreshing ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {error && (
        <div className="bg-destructive/10 border border-destructive text-destructive px-4 py-3 rounded-md flex items-center">
          <AlertTriangle className="w-5 h-5 mr-2" />
          {error}
        </div>
      )}

      {/* Stats Cards */}
      {data && (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="bg-card border rounded-xl p-4 shadow-sm">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Total Storage</p>
                <h3 className="text-2xl font-bold mt-1">{formatBytes(data.stats.totalStoredBytes)}</h3>
              </div>
              <Database className="w-5 h-5 text-primary" />
            </div>
            <div className={`mt-2 text-xs font-medium px-2 py-0.5 rounded-full inline-block ${data.stats.systemHealth === 'Healthy' ? 'bg-green-100 text-green-800' :
              data.stats.systemHealth === 'Warning' ? 'bg-yellow-100 text-yellow-800' :
                'bg-red-100 text-red-800'
              }`}>
              {data.stats.systemHealth}
            </div>
          </div>

          <div className="bg-card border rounded-xl p-4 shadow-sm">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Devices / Shares</p>
                <h3 className="text-2xl font-bold mt-1">{data.stats.totalDevices} / {data.stats.totalShares}</h3>
              </div>
              <Server className="w-5 h-5 text-blue-500" />
            </div>
          </div>

          <div className="bg-card border rounded-xl p-4 shadow-sm">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Running Jobs</p>
                <h3 className="text-2xl font-bold mt-1">{data.stats.runningJobs}</h3>
              </div>
              <Activity className={`w-5 h-5 ${data.stats.runningJobs > 0 ? 'text-blue-500 animate-pulse' : 'text-gray-400'}`} />
            </div>
          </div>

          <div className="bg-card border rounded-xl p-4 shadow-sm">
            <div className="flex justify-between items-start">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Failures (24h)</p>
                <h3 className="text-2xl font-bold mt-1">{data.stats.recentFailures}</h3>
              </div>
              <AlertTriangle className={`w-5 h-5 ${data.stats.recentFailures > 0 ? 'text-red-500' : 'text-gray-400'}`} />
            </div>
          </div>
        </div>
      )}

      {/* Host Summary Table */}
      <div className="bg-card border rounded-xl shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b bg-muted/30">
          <h2 className="font-semibold flex items-center">
            <Server className="w-5 h-5 mr-2" />
            Host Summary
          </h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground text-left">
              <tr>
                <th className="px-6 py-3 font-medium">Host / Share</th>
                <th className="px-6 py-3 font-medium">Status</th>
                <th className="px-6 py-3 font-medium">Last Backup</th>
                <th className="px-6 py-3 font-medium">Last Size</th>
                <th className="px-6 py-3 font-medium">Next Run</th>
                <th className="px-6 py-3 font-medium text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {data?.devices.map((device) => (
                <>
                  {/* Device Header Row */}
                  <tr key={device.id} className="bg-muted/10 font-medium">
                    <td className="px-6 py-3 flex items-center gap-2">
                      {device.type === 'SMB' ? <FolderOpen className="w-4 h-4 text-orange-500" /> : <Server className="w-4 h-4 text-blue-500" />}
                      <Link to={`/devices/${device.id}`} className="hover:underline">{device.name}</Link>
                    </td>
                    <td className="px-6 py-3" colSpan={5}>
                      <span className="text-xs text-muted-foreground">Protocol: {device.type}</span>
                    </td>
                  </tr>

                  {/* Share Rows */}
                  {device.shares.map((share) => (
                    <tr key={share.id} className="hover:bg-muted/5 group">
                      <td className="px-6 py-3 pl-12 text-muted-foreground flex items-center gap-2">
                        <HardDrive className="w-4 h-4" />
                        <span className="truncate max-w-[200px]" title={share.name}>{share.name}</span>
                      </td>
                      <td className="px-6 py-3">
                        <StatusBadge status={share.status} />
                      </td>
                      <td className="px-6 py-3 text-muted-foreground">
                        <div className="flex flex-col">
                          <span>{formatDate(share.lastBackupTime)}</span>
                          {share.lastBackupId && (
                            <span className="text-xs font-mono text-blue-500 cursor-pointer" title={share.lastBackupId}>
                              {share.lastBackupId.substring(0, 8)}
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-6 py-3 text-muted-foreground">
                        {share.fileCount > 0 ? (
                          <div className="flex flex-col">
                            <span>{formatBytes(share.totalSize)}</span>
                            <span className="text-xs">{share.fileCount} files</span>
                          </div>
                        ) : '0 B'}
                      </td>
                      <td className="px-6 py-3 text-muted-foreground">
                        {share.nextBackupTime ? (
                          <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            {formatDate(share.nextBackupTime)}
                          </div>
                        ) : '-'}
                      </td>
                      <td className="px-6 py-3 text-right">
                        <div className="flex justify-end gap-1 opacity-100 sm:opacity-0 sm:group-hover:opacity-100 transition-opacity">
                          {share.status === 'Running' ? (
                            <button
                              onClick={() => share.lastJobId && handleCancelJob(share.lastJobId)}
                              className="p-1.5 text-red-500 hover:bg-red-50 rounded"
                              title="Cancel Backup"
                              disabled={cancellingJobId === share.lastJobId}
                            >
                              <StopCircle className={`w-4 h-4 ${cancellingJobId === share.lastJobId ? 'animate-pulse' : ''}`} />
                            </button>
                          ) : (
                            <button
                              onClick={() => handleTriggerBackup(device.id, share.id)}
                              className="p-1.5 text-blue-500 hover:bg-blue-50 rounded"
                              title="Backup Now"
                            >
                              <Play className="w-4 h-4" />
                            </button>
                          )}

                          {share.lastBackupId && (
                            <Link
                              to={`/backups/${share.lastBackupId}/browse?deviceId=${device.id}&shareId=${share.id}`}
                              className="p-1.5 text-gray-500 hover:bg-gray-100 rounded"
                              title="Browse Files"
                            >
                              <FolderOpen className="w-4 h-4" />
                            </Link>
                          )}

                          <Link
                            to={`/devices/${device.id}`} // Logs view not linkable directly yet
                            className="p-1.5 text-gray-500 hover:bg-gray-100 rounded"
                            title="View Details"
                          >
                            <FileText className="w-4 h-4" />
                          </Link>
                        </div>
                      </td>
                    </tr>
                  ))}

                  {device.shares.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-6 py-3 pl-12 text-muted-foreground italic text-xs">
                        No shares configured
                      </td>
                    </tr>
                  )}
                </>
              ))}

              {data?.devices.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-muted-foreground">
                    <div className="flex flex-col items-center">
                      <Server className="w-8 h-8 opacity-20 mb-2" />
                      No devices configured. <Link to="/devices" className="text-primary hover:underline">Add your first device</Link>.
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const styles = {
    Success: 'bg-green-100 text-green-700 border-green-200',
    Failed: 'bg-red-100 text-red-700 border-red-200',
    Running: 'bg-blue-100 text-blue-700 border-blue-200 animate-pulse',
    Warning: 'bg-yellow-100 text-yellow-700 border-yellow-200',
    Disabled: 'bg-gray-100 text-gray-500 border-gray-200',
    Pending: 'bg-gray-50 text-gray-400 border-gray-100',
    Unknown: 'bg-gray-50 text-gray-400 border-gray-100',
  };

  const icons = {
    Success: CheckCircle,
    Failed: XCircle,
    Running: RefreshCw,
    Warning: AlertTriangle,
    Disabled: StopCircle,
    Pending: Clock,
    Unknown: Clock,
  };

  const style = styles[status as keyof typeof styles] || styles.Unknown;
  const Icon = icons[status as keyof typeof icons] || icons.Unknown;

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${style}`}>
      <Icon className={`w-3 h-3 mr-1 ${status === 'Running' ? 'animate-spin' : ''}`} />
      {status}
    </span>
  );
}
