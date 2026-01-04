import React from 'react';

export type InputVariant = 'default' | 'error' | 'success';

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  variant?: InputVariant;
  label?: string;
  helperText?: string;
}

export function Input({ 
  variant = 'default', 
  label, 
  helperText, 
  className = '', 
  id,
  ...props 
}: InputProps) {
  const inputId = id || label?.toLowerCase().replace(/\s+/g, '-');
  
  const baseStyles = 'w-full px-3 py-2 rounded-lg border bg-input-background transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1';
  
  const variantStyles = {
    default: 'border-input focus:border-[var(--input-focus)] focus:ring-[var(--input-focus)]',
    error: 'border-error focus:border-error focus:ring-error bg-error-bg/10',
    success: 'border-success focus:border-success focus:ring-success bg-success-bg/10',
  };
  
  const helperTextStyles = {
    default: 'text-muted-foreground',
    error: 'text-error-fg',
    success: 'text-success-fg',
  };
  
  return (
    <div className="w-full">
      {label && (
        <label htmlFor={inputId} className="block mb-1.5">
          {label}
        </label>
      )}
      <input
        id={inputId}
        className={`${baseStyles} ${variantStyles[variant]} ${className}`}
        {...props}
      />
      {helperText && (
        <p className={`mt-1.5 ${helperTextStyles[variant]}`}>
          {helperText}
        </p>
      )}
    </div>
  );
}

interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  options: { value: string; label: string }[];
}

export function Select({ label, options, className = '', id, ...props }: SelectProps) {
  const selectId = id || label?.toLowerCase().replace(/\s+/g, '-');
  
  return (
    <div className="w-full">
      {label && (
        <label htmlFor={selectId} className="block mb-1.5">
          {label}
        </label>
      )}
      <select
        id={selectId}
        className={`w-full px-3 py-2 rounded-lg border border-input bg-input-background transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1 focus:border-[var(--input-focus)] focus:ring-[var(--input-focus)] ${className}`}
        {...props}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}
