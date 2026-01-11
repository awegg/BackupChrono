import { useState, useEffect, useRef } from 'react';
import { RefreshCw, AlertTriangle } from 'lucide-react';
import { dashboardService } from '../services/dashboardService';
import { backupService } from '../services/deviceService';
import { retentionService } from '../services/retentionService';
import { RetentionPanel } from '../components/RetentionPanel';
import { DashboardHeader } from '../components/DashboardHeader';
import { DashboardMetrics } from '../components/DashboardMetrics';
import { ActiveJobsTable } from '../components/ActiveJobsTable';
import { RecentlyCompletedTable } from '../components/RecentlyCompletedTable';
import { BackupJob, Backup, BackupStatus, RetentionLastRun, RetentionRunResult } from '../types';

export default function Dashboard() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [backendOffline, setBackendOffline] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(() => new Date().toLocaleTimeString());
  const lastUpdateTimeRef = useRef(Date.now());
  const [isConnected, setIsConnected] = useState(true);
  
  // Dashboard data
  const [stats, setStats] = useState({
    activeJobs: 0,
    queuedJobs: 0,
    completedJobs: 0,
    failedJobs: 0,
    avgSpeed: '0 MB/s',
    dataToday: '0 B',
  });
  const [activeJobs, setActiveJobs] = useState<BackupJob[]>([]);
  const [recentBackups, setRecentBackups] = useState<Backup[]>([]);
  const [retentionLastRun, setRetentionLastRun] = useState<RetentionLastRun | null>(null);
  const [retentionResults, setRetentionResults] = useState<RetentionRunResult[]>([]);
  const [retentionRunning, setRetentionRunning] = useState(false);
  const [retentionSummaryLoading, setRetentionSummaryLoading] = useState(true);
  const [retentionError, setRetentionError] = useState<string | null>(null);

  const loadDashboardData = async () => {
    try {
      setError(null);
      
      // Load all dashboard data - if this fails, backend is down
      const [statsData, jobsData, backupsData] = await Promise.all([
        dashboardService.getStats(),
        dashboardService.getActiveJobs(),
        dashboardService.getRecentBackups(10),
      ]);
      
      setStats(statsData);
      setActiveJobs(jobsData);
      setRecentBackups(backupsData);
      setLastUpdated(new Date().toLocaleTimeString());
      lastUpdateTimeRef.current = Date.now();
      setIsConnected(true);
      setBackendOffline(false);
      setLoading(false);
    } catch (err) {
      setError('Failed to load dashboard data. Backend server may not be running.');
      setBackendOffline(true);
      setIsConnected(false);
      console.error(err);
      setLoading(false);
    }
  };

  const loadRetentionSummary = async () => {
    try {
      setRetentionSummaryLoading(true);
      const summary = await retentionService.getLastRun();
      setRetentionLastRun(summary);
      setRetentionError(null);
    } catch (err: any) {
      console.error('Failed to load retention summary:', err);
      const isNetworkError = !err.response;
      const errorMessage = isNetworkError 
        ? 'Unable to connect to backend server'
        : `Failed to load retention summary: ${err.response?.data?.message || err.message}`;
      setRetentionError(errorMessage);
    } finally {
      setRetentionSummaryLoading(false);
    }
  };

  const handleRunRetention = async (dryRun = false) => {
    try {
      setRetentionRunning(true);
      setRetentionError(null);
      const results = await retentionService.runRetention(dryRun);
      setRetentionResults(results);
      await loadRetentionSummary();
    } catch (err: any) {
      console.error('Failed to execute retention policy:', err);
      const isNetworkError = !err.response;
      const errorMessage = isNetworkError
        ? 'Unable to connect to backend server'
        : `Retention policy execution failed: ${err.response?.data?.detail || err.response?.data?.error || err.message}`;
      setRetentionError(errorMessage);
    } finally {
      setRetentionRunning(false);
    }
  };

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDashboardData();
    loadRetentionSummary();
    
    // Refresh data every 5 seconds
    const dataInterval = setInterval(() => {
      loadDashboardData();
    }, 5000);

    // Refresh retention summary every minute
    const retentionInterval = setInterval(() => {
      loadRetentionSummary();
    }, 60000);

    // Check connection status every second
    const connectionCheckInterval = setInterval(() => {
      const timeSinceLastUpdate = Date.now() - lastUpdateTimeRef.current;
      setIsConnected(timeSinceLastUpdate < 5000);
    }, 1000);

    return () => {
      clearInterval(dataInterval);
      clearInterval(connectionCheckInterval);
      clearInterval(retentionInterval);
    };
  }, []); // Empty dependency array - only run once on mount

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <RefreshCw className="w-6 h-6 animate-spin text-primary" />
      </div>
    );
  }

  // Transform API data for components
  const activeJobsData = activeJobs.map(job => ({
    id: job.id,
    deviceId: job.deviceId,
    deviceName: job.deviceName || 'Unknown Device',
    status: job.status as 'Running' | 'Pending',
    path: job.shareName || 'N/A',
    progress: job.filesProcessed && job.bytesTransferred 
      ? Math.min(Math.round((job.bytesTransferred / 1024 / 1024 / 1024) * 10), 100)
      : 0,
    currentFile: `Processing... (${job.filesProcessed || 0} files)`,
    speed: job.startedAt && job.bytesTransferred
      ? `${Math.round(dashboardService.calculateSpeed(job.bytesTransferred, job.startedAt))} MB/s`
      : '0 MB/s',
    eta: job.startedAt 
      ? dashboardService.formatDuration(job.startedAt)
      : 'Calculating...',
  }));

  const completedBackupsData = recentBackups.map(backup => ({
    backupId: backup.id,
    deviceId: backup.deviceId,
    shareId: backup.shareId,
    deviceName: backup.deviceName,
    path: backup.shareName || Object.values(backup.sharesPaths)[0] || 'N/A',
    status: backup.status === BackupStatus.Success ? 'Success' as const : 'Warning' as const,
    duration: backup.duration || 'Unknown',
    dataTransferred: dashboardService.formatBytes(backup.dataAdded || 0),
    completedAt: new Date(backup.timestamp).toISOString(),
  }));

  const handleStopJob = async (jobId: string) => {
    try {
      const job = activeJobs.find(j => j.id === jobId);
      if (!job) return;
      
      await backupService.cancelJob(jobId);
      await loadDashboardData();
    } catch (err) {
      console.error('Failed to stop job:', err);
      setError('Failed to cancel job. Please try again.');
    }
  };

  return (
    <div className="space-y-6">
      <DashboardHeader
        title="Live Monitoring Dashboard"
        subtitle="Real-time backup job status and performance metrics"
        lastUpdated={lastUpdated}
        isConnected={isConnected}
      />

      {error && (
        <div
          className={`border px-4 py-3 rounded-lg ${
            backendOffline
              ? 'bg-status-warning-bg border-status-warning text-status-warning-fg'
              : 'bg-status-error-bg border-status-error text-status-error-fg'
          }`}
        >
          <div className="flex items-center">
            <AlertTriangle className="w-5 h-5 mr-2" />
            <div>
              <div className="font-semibold">
                {backendOffline ? 'Backend Server Offline' : 'Error'}
              </div>
              <div className="text-sm">{error}</div>
              {backendOffline && (
                <div className="text-sm mt-2">
                  Expected backend URL:{' '}
                  <code className="bg-status-warning px-1 rounded">
                    {import.meta.env.VITE_API_URL || 'http://localhost:5000'}
                  </code>
                  <br />
                  <button
                    onClick={loadDashboardData}
                    className="mt-2 underline hover:no-underline"
                  >
                    Retry connection
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Metrics Section */}
      <DashboardMetrics
        activeJobs={stats.activeJobs}
        queuedJobs={stats.queuedJobs}
        completedJobs={stats.completedJobs}
        failedJobs={stats.failedJobs}
        avgSpeed={stats.avgSpeed}
        dataToday={stats.dataToday}
      />

      <RetentionPanel
        lastRun={retentionLastRun}
        recentResults={retentionResults}
        onRunRetention={handleRunRetention}
        running={retentionRunning}
        loadingSummary={retentionSummaryLoading}
        error={retentionError}
      />

      {/* Active Backup Jobs Section */}
      <div>
        <h2 className="text-xl font-semibold text-foreground mb-4">Active Backup Jobs</h2>
        <ActiveJobsTable jobs={activeJobsData} onStopJob={handleStopJob} />
      </div>

      {/* Recently Completed Section */}
      <div>
        <h2 className="text-xl font-semibold text-foreground mb-4">Recently Completed</h2>
        <RecentlyCompletedTable backups={completedBackupsData} />
      </div>
    </div>
  );
}

