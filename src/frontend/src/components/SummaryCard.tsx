import React from 'react';
import { AlertTriangle, Database, XCircle } from 'lucide-react';

interface SummaryCardProps {
  title: string;
  value: string | number;
  subtitle: string;
  variant: 'warning' | 'info' | 'error';
  onClick?: () => void;
}

const iconMap = {
  warning: AlertTriangle,
  info: Database,
  error: XCircle,
};

const colorMap = {
  warning: {
    bg: 'bg-amber-500/10 dark:bg-amber-500/20',
    icon: 'text-amber-500',
  },
  info: {
    bg: 'bg-blue-500/10 dark:bg-blue-500/20',
    icon: 'text-blue-500',
  },
  error: {
    bg: 'bg-red-500/10 dark:bg-red-500/20',
    icon: 'text-red-500',
  },
};

export const SummaryCard: React.FC<SummaryCardProps> = ({
  title,
  value,
  subtitle,
  variant,
  onClick,
}) => {
  const Icon = iconMap[variant];
  const colors = colorMap[variant];

  return (
    <button
      onClick={onClick}
      className="bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 p-3 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors text-left w-full"
    >
      <div className="flex items-center gap-3">
        <div className={`${colors.bg} p-2 rounded-lg flex-shrink-0`}>
          <Icon className={`w-5 h-5 ${colors.icon}`} />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-xs text-slate-600 dark:text-slate-400">
            {title}
          </p>
          <div className="flex items-baseline gap-2">
            <p className="text-xl font-semibold text-slate-900 dark:text-white">
              {value}
            </p>
            <p className="text-xs text-slate-500 dark:text-slate-500 truncate">
              {subtitle}
            </p>
          </div>
        </div>
      </div>
    </button>
  );
};
