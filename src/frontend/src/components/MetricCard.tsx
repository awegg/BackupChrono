import { LucideIcon } from 'lucide-react';

interface MetricCardProps {
  label: string;
  value: string | number;
  icon: LucideIcon;
  variant?: 'default' | 'success' | 'warning' | 'error';
  subtitle?: string;
}

export function MetricCard({ label, value, icon: Icon, variant = 'default', subtitle }: MetricCardProps) {
  const variantStyles = {
    default: 'text-muted-foreground',
    success: 'text-status-success',
    warning: 'text-status-warning',
    error: 'text-status-error',
  };

  const valueTextStyles = {
    default: 'text-foreground',
    success: 'text-status-success',
    warning: 'text-foreground',
    error: 'text-status-error',
  };

  const bgStyles = {
    default: '',
    success: 'bg-status-success-bg',
    warning: 'bg-status-warning-bg',
    error: 'bg-status-error-bg',
  };

  return (
    <div className="bg-card rounded-lg shadow-sm p-4 border border-border">
      <div className="flex items-start justify-between mb-2">
        <span className="text-sm text-muted-foreground uppercase tracking-wide">
          {label}
        </span>
        <Icon className={`w-4 h-4 ${variantStyles[variant]}`} />
      </div>
      
      <div className={`text-2xl font-medium ${valueTextStyles[variant]}`}>
        {value}
      </div>
      
      {subtitle && (
        <div className={`mt-1 text-xs px-2 py-0.5 rounded inline-block ${bgStyles[variant]}`}>
          <span className={`${valueTextStyles[variant]} font-medium`}>{subtitle}</span>
        </div>
      )}
    </div>
  );
}
