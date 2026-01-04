import { Activity, Clock, CheckCircle, XCircle, Gauge, Database } from 'lucide-react';
import { MetricCard } from './MetricCard';

interface DashboardMetricsProps {
  activeJobs: number;
  queuedJobs: number;
  completedJobs: number;
  failedJobs: number;
  avgSpeed: string;
  dataToday: string;
}

export function DashboardMetrics({
  activeJobs,
  queuedJobs,
  completedJobs,
  failedJobs,
  avgSpeed,
  dataToday,
}: DashboardMetricsProps) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
      <MetricCard
        label="Active Jobs"
        value={activeJobs}
        icon={Activity}
        variant="default"
      />
      
      <MetricCard
        label="Queued"
        value={queuedJobs}
        icon={Clock}
        variant="default"
      />
      
      <MetricCard
        label="Completed"
        value={completedJobs}
        icon={CheckCircle}
        variant="success"
      />
      
      <MetricCard
        label="Failed"
        value={failedJobs}
        icon={XCircle}
        variant={failedJobs > 0 ? 'error' : 'default'}
        subtitle={failedJobs > 0 ? 'Requires attention' : undefined}
      />
      
      <MetricCard
        label="Avg Speed"
        value={avgSpeed}
        icon={Gauge}
        variant="default"
      />
      
      <MetricCard
        label="Data Today"
        value={dataToday}
        icon={Database}
        variant="default"
      />
    </div>
  );
}
