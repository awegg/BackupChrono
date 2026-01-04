import { useState, useEffect, useRef } from 'react';
import { RefreshCw, AlertTriangle } from 'lucide-react';
import { healthService } from '../services/healthService';
import { dashboardService } from '../services/dashboardService';
import { DashboardHeader } from '../components/DashboardHeader';
import { DashboardMetrics } from '../components/DashboardMetrics';
import { ActiveJobsTable } from '../components/ActiveJobsTable';
import { RecentlyCompletedTable } from '../components/RecentlyCompletedTable';
import { BackupJob, Backup, BackupStatus } from '../types';

export default function Dashboard() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [backendOffline, setBackendOffline] = useState(false);
  const [lastUpdated, setLastUpdated] = useState(new Date().toLocaleTimeString());
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

  const checkBackendHealth = async () => {
    const isHealthy = await healthService.checkAvailability();
    setBackendOffline(!isHealthy);
    return isHealthy;
  };

  const loadDashboardData = async () => {
    try {
      setError(null);
      
      // Check backend health first
      const isBackendHealthy = await checkBackendHealth();
      if (!isBackendHealthy) {
        setError('Backend server is not running. Please start the API server.');
        setLoading(false);
        return;
      }

      // Load all dashboard data
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
      setLoading(false);
    } catch (err) {
      setError('Failed to load dashboard data');
      setBackendOffline(true);
      console.error(err);
      setLoading(false);
    }
  };

  useEffect(() => {
    setLoading(true);
    loadDashboardData();
    
    // Refresh data every 5 seconds
    const dataInterval = setInterval(() => {
      loadDashboardData();
    }, 5000);

    // Check connection status every second
    const connectionCheckInterval = setInterval(() => {
      const timeSinceLastUpdate = Date.now() - lastUpdateTimeRef.current;
      setIsConnected(timeSinceLastUpdate < 5000);
    }, 1000);

    return () => {
      clearInterval(dataInterval);
      clearInterval(connectionCheckInterval);
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
      : 'Unknown',
  }));

  const completedBackupsData = recentBackups.map(backup => ({
    deviceId: backup.deviceId,
    deviceName: backup.deviceName,
    path: backup.shareName || Object.values(backup.sharesPaths)[0] || 'N/A',
    status: backup.status === BackupStatus.Success ? 'Success' as const : 'Warning' as const,
    duration: backup.duration || 'Unknown',
    dataTransferred: dashboardService.formatBytes(backup.dataAdded || 0),
    completedAt: new Date(backup.timestamp).toLocaleTimeString(),
  }));

  const handleStopJob = async (deviceId: string) => {
    try {
      // TODO: Implement API call to cancel job
      console.log('Stopping job for device:', deviceId);
      // await dashboardService.cancelJob(deviceId);
      // Refresh the data after canceling
      // await loadDashboardData();
    } catch (err) {
      console.error('Failed to stop job:', err);
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

