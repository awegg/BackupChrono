import { useMemo } from 'react';
import { Play, TestTube, History, Recycle, AlertTriangle, Loader2 } from 'lucide-react';
import { RetentionLastRun, RetentionRunResult } from '../types';

interface RetentionPanelProps {
  lastRun: RetentionLastRun | null;
  recentResults: RetentionRunResult[];
  onRunRetention: (dryRun?: boolean) => void;
  running: boolean;
  loadingSummary: boolean;
  error?: string | null;
}

const formatBytes = (bytes: number): string => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const idx = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  const value = bytes / Math.pow(1024, idx);
  return `${value.toFixed(value >= 10 ? 0 : 1)} ${units[idx]}`;
};

const formatRelative = (timestamp: string): string => {
  const date = new Date(timestamp);
  const now = Date.now();
  const diffMs = date.getTime() - now;
  const absMs = Math.abs(diffMs);

  const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
  const minute = 60 * 1000;
  const hour = 60 * minute;
  const day = 24 * hour;

  if (absMs < hour) {
    return rtf.format(Math.round(diffMs / minute), 'minute');
  }
  if (absMs < day) {
    return rtf.format(Math.round(diffMs / hour), 'hour');
  }
  return rtf.format(Math.round(diffMs / day), 'day');
};

export function RetentionPanel({
  lastRun,
  recentResults,
  onRunRetention,
  running,
  loadingSummary,
  error,
}: RetentionPanelProps) {
  const headline = lastRun
    ? `${formatRelative(lastRun.timestamp)} · ${new Date(lastRun.timestamp).toLocaleString()}`
    : 'Not run yet';

  const aggregated = useMemo(() => {
    if (!lastRun) {
      return {
        pruned: recentResults.reduce((sum, r) => sum + r.snapshotsPruned, 0),
        reclaimed: recentResults.reduce((sum, r) => sum + r.spaceReclaimedBytes, 0),
      };
    }
    return {
      pruned: lastRun.totalSnapshotsPruned,
      reclaimed: lastRun.totalSpaceReclaimedBytes,
    };
  }, [lastRun, recentResults]);

  return (
    <section className="bg-card border border-border rounded-lg shadow-sm p-6">
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-wide text-muted-foreground">Retention Policy</p>
          <h2 className="text-xl font-semibold text-foreground">Snapshot Cleanup</h2>
          <p className="text-sm text-muted-foreground mt-1">
            Prune old snapshots and reclaim space according to the configured policies.
          </p>
        </div>
        <div className="flex gap-3">
          <button
            className="inline-flex items-center gap-2 px-4 py-2 rounded-md bg-primary text-primary-foreground shadow-sm hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed"
            onClick={() => onRunRetention(false)}
            disabled={running}
            aria-label="Run retention policy now"
          >
            {running ? <Loader2 className="w-4 h-4 animate-spin" /> : <Play className="w-4 h-4" />}
            Run now
          </button>
          <button
            className="inline-flex items-center gap-2 px-4 py-2 rounded-md border border-border text-foreground hover:bg-muted disabled:opacity-50 disabled:cursor-not-allowed"
            onClick={() => onRunRetention(true)}
            disabled={running}
            aria-label="Simulate retention policy"
          >
            <TestTube className="w-4 h-4" />
            Dry run
          </button>
        </div>
      </div>

      {error && (
        <div className="mt-4 flex items-start gap-2 rounded-md border border-status-error bg-status-error-bg/30 px-3 py-2 text-sm text-status-error">
          <AlertTriangle className="w-4 h-4 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      <div className="grid md:grid-cols-3 gap-4 mt-6">
        <div className="p-4 rounded-lg border border-border bg-muted/30">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <History className="w-4 h-4" />
            Last run
          </div>
          <div className="text-lg font-semibold text-foreground mt-1">
            {loadingSummary ? 'Loading…' : headline}
          </div>
        </div>
        <div className="p-4 rounded-lg border border-border bg-muted/30">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Recycle className="w-4 h-4" />
            Snapshots pruned
          </div>
          <div className="text-lg font-semibold text-foreground mt-1">
            {aggregated.pruned ?? 0}
          </div>
        </div>
        <div className="p-4 rounded-lg border border-border bg-muted/30">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Recycle className="w-4 h-4" />
            Space reclaimed
          </div>
          <div className="text-lg font-semibold text-foreground mt-1">
            {formatBytes(aggregated.reclaimed ?? 0)}
          </div>
        </div>
      </div>

      {recentResults.length > 0 && (
        <div className="mt-6">
          <div className="text-sm text-muted-foreground mb-2">Latest execution results</div>
          <div className="border border-border rounded-lg divide-y divide-border">
            {recentResults.slice(0, 5).map((result, idx) => (
              <div key={`${result.deviceName}-${result.shareName ?? 'all'}-${idx}`} className="flex justify-between items-start p-3">
                <div>
                  <div className="font-medium text-foreground">
                    {result.deviceName}
                    {result.shareName ? ` / ${result.shareName}` : ''}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {result.error
                      ? result.error
                      : `${result.snapshotsPruned} pruned · ${formatBytes(result.spaceReclaimedBytes)}`}
                  </div>
                </div>
                <span className={`text-sm font-medium ${result.error ? 'text-status-error' : 'text-status-success'}`}>
                  {result.error ? 'Error' : 'Completed'}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}
