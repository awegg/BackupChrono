import { Server, Loader2, StopCircle } from 'lucide-react';

interface ActiveJob {
  id: string;
  deviceId: string;
  deviceName: string;
  status: 'Running' | 'Pending';
  path: string;
  progress: number;
  currentFile: string;
  speed: string;
  eta: string;
}

interface ActiveJobsTableProps {
  jobs: ActiveJob[];
  onStopJob?: (jobId: string) => void;
}

export function ActiveJobsTable({ jobs, onStopJob = () => {} }: ActiveJobsTableProps) {
  if (jobs.length === 0) {
    return (
      <div className="bg-card rounded-lg shadow-sm p-8 border border-border">
        <div className="text-center text-muted-foreground">
          No active backup jobs
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
              Status
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Path
            </th>
            <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Progress
            </th>
            <th className="text-right px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Speed
            </th>
            <th className="text-right px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              ETA
            </th>
            <th className="text-right px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Actions
            </th>
          </tr>
        </thead>
        <tbody>
          {jobs.map((job) => (
            <tr
              key={job.id}
              className="border-b border-border last:border-0 hover:bg-muted/50 transition-colors"
            >
              <td className="px-4 py-3">
                <div className="flex items-center gap-2">
                  <Server className="w-4 h-4 text-primary" />
                  <div>
                    <div className="font-medium text-foreground">{job.deviceName}</div>
                    <div className="text-xs text-muted-foreground truncate max-w-xs">
                      Current: {job.currentFile}
                    </div>
                  </div>
                </div>
              </td>
              
              <td className="px-4 py-3">
                <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-sm bg-primary/10 text-primary">
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  {job.status}
                </span>
              </td>
              
              <td className="px-4 py-3">
                <span className="font-mono text-sm text-foreground">{job.path}</span>
              </td>
              
              <td className="px-4 py-3">
                <div className="flex items-center gap-2">
                  <div className="flex-1 bg-muted rounded-full h-2 overflow-hidden">
                    <div
                      className="bg-primary h-full transition-all duration-300"
                      style={{ width: `${job.progress}%` }}
                    />
                  </div>
                  <span className="text-sm font-medium text-foreground min-w-[3rem] text-right">
                    {job.progress}%
                  </span>
                </div>
              </td>
              
              <td className="px-4 py-3 text-right">
                <span className="text-sm font-medium text-foreground">{job.speed}</span>
              </td>
              
              <td className="px-4 py-3 text-right">
                <span className="text-sm text-muted-foreground">{job.eta}</span>
              </td>
              
              <td className="px-4 py-3 text-right">
                <button
                  onClick={() => onStopJob(job.id)}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-status-error hover:text-status-error-fg bg-status-error-bg hover:bg-status-error/20 rounded-md transition-colors"
                  title="Stop backup job"
                >
                  <StopCircle className="w-4 h-4" />
                  Stop
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
