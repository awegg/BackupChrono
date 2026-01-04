import React from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  children: React.ReactNode;
}

export function Button({ 
  variant = 'primary', 
  size = 'md', 
  className = '', 
  children, 
  ...props 
}: ButtonProps) {
  const baseStyles = 'inline-flex items-center justify-center rounded-lg transition-colors focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed';
  
  const variantStyles = {
    primary: 'bg-primary text-primary-foreground hover:bg-[var(--primary-hover)] focus:ring-primary',
    secondary: 'bg-secondary text-secondary-foreground hover:bg-[var(--secondary-hover)] focus:ring-secondary border border-border',
    danger: 'bg-destructive text-destructive-foreground hover:bg-[var(--destructive-hover)] focus:ring-destructive',
  };
  
  const sizeStyles = {
    sm: 'px-3 py-1.5 gap-1.5',
    md: 'px-4 py-2 gap-2',
    lg: 'px-6 py-3 gap-2',
  };
  
  return (
    <button
      className={`${baseStyles} ${variantStyles[variant]} ${sizeStyles[size]} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}
