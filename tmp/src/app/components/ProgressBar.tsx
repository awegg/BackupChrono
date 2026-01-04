import React from 'react';

export type ProgressVariant = 'default' | 'success' | 'warning' | 'error';

interface ProgressBarProps {
  value: number;
  max?: number;
  variant?: ProgressVariant;
  label?: string;
  showPercentage?: boolean;
  className?: string;
}

export function ProgressBar({ 
  value, 
  max = 100, 
  variant = 'default', 
  label, 
  showPercentage = true,
  className = '' 
}: ProgressBarProps) {
  const percentage = Math.round((value / max) * 100);
  
  const variantStyles = {
    default: 'bg-primary',
    success: 'bg-success',
    warning: 'bg-warning',
    error: 'bg-error',
  };
  
  return (
    <div className={`w-full ${className}`}>
      {(label || showPercentage) && (
        <div className="flex justify-between items-center mb-2">
          {label && <span className="text-muted-foreground">{label}</span>}
          {showPercentage && <span className="text-muted-foreground">{percentage}%</span>}
        </div>
      )}
      <div className="w-full h-2 bg-muted rounded-full overflow-hidden">
        <div
          className={`h-full transition-all duration-300 ${variantStyles[variant]}`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  );
}
