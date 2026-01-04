import { useState, useEffect } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from './Card';
import { StatusBadge } from './StatusBadge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './Table';
import { ProgressBar } from './ProgressBar';
import { Server, Activity, Clock, AlertCircle } from 'lucide-react';

interface BackupJob {
  id: number;
  device: string;
  path: string;
  status: 'running' | 'success' | 'warning' | 'error' | 'pending';
  progress: number;
  startTime: string;
  currentFile: string;
  speed: string;
  eta: string;
}

export function MonitoringDashboard() {
  const [jobs, setJobs] = useState<BackupJob[]>([
    {
      id: 1,
      device: 'prod-server-01',
      path: '/var/www/html',
      status: 'running',
      progress: 67,
      startTime: '09:15:23',
      currentFile: '/var/www/html/wp-content/uploads/2026/01/image-4521.jpg',
      speed: '125 MB/s',
      eta: '4m 32s'
    },
    {
      id: 2,
      device: 'nas-storage',
      path: '/mnt/data/documents',
      status: 'running',
      progress: 34,
      startTime: '09:00:15',
      currentFile: '/mnt/data/documents/projects/Q4-2025/reports/annual-summary.xlsx',
      speed: '89 MB/s',
      eta: '18m 45s'
    },
    {
      id: 3,
      device: 'db-server-02',
      path: '/var/lib/postgresql',
      status: 'pending',
      progress: 0,
      startTime: '—',
      currentFile: 'Waiting in queue...',
      speed: '—',
      eta: '—'
    },
    {
      id: 4,
      device: 'mail-server',
      path: '/var/mail',
      status: 'success',
      progress: 100,
      startTime: '08:45:10',
      currentFile: 'Completed successfully',
      speed: '156 MB/s',
      eta: 'Done'
    }
  ]);

  const [stats, setStats] = useState({
    activeJobs: 2,
    queuedJobs: 1,
    completedToday: 38,
    failedToday: 1,
    avgSpeed: '123 MB/s',
    totalDataToday: '2.3 TB'
  });

  const [lastUpdate, setLastUpdate] = useState(new Date());

  // Simulate real-time updates
  useEffect(() => {
    const interval = setInterval(() => {
      setJobs(prevJobs => 
        prevJobs.map(job => {
          if (job.status === 'running') {
            const newProgress = Math.min(job.progress + Math.random() * 5, 100);
            const isComplete = newProgress >= 100;
            
            return {
              ...job,
              progress: newProgress,
              status: isComplete ? 'success' : 'running',
              currentFile: isComplete ? 'Completed successfully' : job.currentFile,
              eta: isComplete ? 'Done' : job.eta,
              speed: isComplete ? job.speed : `${Math.floor(Math.random() * 50 + 100)} MB/s`
            };
          }
          return job;
        })
      );

      setStats(prev => ({
        ...prev,
        avgSpeed: `${Math.floor(Math.random() * 40 + 100)} MB/s`,
        totalDataToday: `${(2.3 + Math.random() * 0.1).toFixed(1)} TB`
      }));

      setLastUpdate(new Date());
    }, 2000);

    return () => clearInterval(interval);
  }, []);

  const getStatusBadge = (status: BackupJob['status']) => {
    const statusMap: Record<BackupJob['status'], { type: 'success' | 'warning' | 'error' | 'neutral', label: string }> = {
      running: { type: 'neutral', label: 'Running' },
      success: { type: 'success', label: 'Complete' },
      warning: { type: 'warning', label: 'Warning' },
      error: { type: 'error', label: 'Failed' },
      pending: { type: 'neutral', label: 'Pending' }
    };
    const config = statusMap[status];
    return <StatusBadge status={config.type} label={config.label} />;
  };

  return (
    <div className="space-y-6">
      {/* Header with live indicator */}
      <div className="flex items-center justify-between">
        <div>
          <h1>Live Monitoring Dashboard</h1>
          <p className="text-muted-foreground mt-1">Real-time backup job status and performance metrics</p>
        </div>
        <div className="flex items-center gap-2 px-4 py-2 bg-success-bg rounded-lg">
          <div className="w-2 h-2 bg-success rounded-full animate-pulse"></div>
          <span className="text-success-fg">Live • Updated {lastUpdate.toLocaleTimeString()}</span>
        </div>
      </div>

      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-4">
        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <Activity className="w-4 h-4 text-primary" />
              <span className="text-muted-foreground">Active Jobs</span>
            </div>
            <span className="text-2xl font-medium">{stats.activeJobs}</span>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <Clock className="w-4 h-4 text-neutral" />
              <span className="text-muted-foreground">Queued</span>
            </div>
            <span className="text-2xl font-medium">{stats.queuedJobs}</span>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-success">✓</span>
              <span className="text-muted-foreground">Completed</span>
            </div>
            <span className="text-2xl font-medium">{stats.completedToday}</span>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <AlertCircle className="w-4 h-4 text-error" />
              <span className="text-muted-foreground">Failed</span>
            </div>
            <span className="text-2xl font-medium">{stats.failedToday}</span>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-muted-foreground">Avg Speed</span>
            </div>
            <span className="text-2xl font-medium font-[var(--font-mono)]">{stats.avgSpeed}</span>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex flex-col">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-muted-foreground">Data Today</span>
            </div>
            <span className="text-2xl font-medium font-[var(--font-mono)]">{stats.totalDataToday}</span>
          </CardContent>
        </Card>
      </div>

      {/* Active Jobs */}
      <Card>
        <CardHeader>
          <CardTitle>Active & Queued Backup Jobs</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {jobs.map(job => (
            <div key={job.id} className="border border-border rounded-lg p-4 space-y-3">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <div className="flex items-center gap-3 mb-1">
                    <Server className="w-4 h-4 text-muted-foreground" />
                    <span className="font-medium">{job.device}</span>
                    {getStatusBadge(job.status)}
                  </div>
                  <code className="font-[var(--font-mono)] bg-muted px-2 py-1 rounded">
                    {job.path}
                  </code>
                </div>
                <div className="text-right">
                  <div className="text-muted-foreground">Started</div>
                  <div className="font-[var(--font-mono)]">{job.startTime}</div>
                </div>
              </div>

              <ProgressBar
                value={job.progress}
                variant={
                  job.status === 'success' ? 'success' :
                  job.status === 'error' ? 'error' :
                  job.status === 'warning' ? 'warning' : 'default'
                }
                showPercentage={true}
              />

              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-2 border-t border-border">
                <div>
                  <div className="text-muted-foreground mb-1">Current File</div>
                  <div className="font-[var(--font-mono)] truncate" title={job.currentFile}>
                    {job.currentFile}
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground mb-1">Speed</div>
                  <div className="font-[var(--font-mono)]">{job.speed}</div>
                </div>
                <div>
                  <div className="text-muted-foreground mb-1">ETA</div>
                  <div className="font-[var(--font-mono)]">{job.eta}</div>
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Recent Completions */}
      <Card>
        <CardHeader>
          <CardTitle>Recently Completed</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow zebra={false}>
                <TableHead>Device</TableHead>
                <TableHead>Path</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Duration</TableHead>
                <TableHead>Data Transferred</TableHead>
                <TableHead>Completed At</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell><span className="font-medium">mail-server</span></TableCell>
                <TableCell>
                  <code className="font-[var(--font-mono)] bg-muted px-1.5 py-0.5 rounded">/var/mail</code>
                </TableCell>
                <TableCell><StatusBadge status="success" label="Success" /></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">14m 32s</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">67.1 GB</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">09:00:42</span></TableCell>
              </TableRow>
              <TableRow>
                <TableCell><span className="font-medium">workstation-05</span></TableCell>
                <TableCell>
                  <code className="font-[var(--font-mono)] bg-muted px-1.5 py-0.5 rounded">C:\Users\Admin</code>
                </TableCell>
                <TableCell><StatusBadge status="warning" label="Warning" /></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">32m 18s</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">89.3 GB</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">08:47:15</span></TableCell>
              </TableRow>
              <TableRow>
                <TableCell><span className="font-medium">dev-server-03</span></TableCell>
                <TableCell>
                  <code className="font-[var(--font-mono)] bg-muted px-1.5 py-0.5 rounded">/home/developer</code>
                </TableCell>
                <TableCell><StatusBadge status="success" label="Success" /></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">8m 45s</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">23.4 GB</span></TableCell>
                <TableCell><span className="font-[var(--font-mono)]">08:30:22</span></TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
