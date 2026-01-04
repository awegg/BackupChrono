import React from 'react';
import { CircleCheck, CircleAlert, TriangleAlert, Info } from 'lucide-react';

export type StatusType = 'success' | 'warning' | 'error' | 'neutral';

interface StatusBadgeProps {
  status: StatusType;
  label: string;
  showIcon?: boolean;
  className?: string;
}

export function StatusBadge({ status, label, showIcon = true, className = '' }: StatusBadgeProps) {
  const statusConfig = {
    success: {
      bgColor: 'bg-success-bg',
      textColor: 'text-success-fg',
      icon: CircleCheck,
    },
    warning: {
      bgColor: 'bg-warning-bg',
      textColor: 'text-warning-fg',
      icon: TriangleAlert,
    },
    error: {
      bgColor: 'bg-error-bg',
      textColor: 'text-error-fg',
      icon: CircleAlert,
    },
    neutral: {
      bgColor: 'bg-neutral-bg',
      textColor: 'text-neutral-fg',
      icon: Info,
    },
  };

  const config = statusConfig[status];
  const Icon = config.icon;

  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full ${config.bgColor} ${config.textColor} ${className}`}
    >
      {showIcon && <Icon className="w-3.5 h-3.5" />}
      <span className="leading-none">{label}</span>
    </span>
  );
}
