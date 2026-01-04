import React from 'react';

interface TableProps {
  children: React.ReactNode;
  className?: string;
}

export function Table({ children, className = '' }: TableProps) {
  return (
    <div className="w-full overflow-auto border border-border rounded-lg">
      <table className={`w-full ${className}`}>{children}</table>
    </div>
  );
}

interface TableHeaderProps {
  children: React.ReactNode;
  className?: string;
}

export function TableHeader({ children, className = '' }: TableHeaderProps) {
  return <thead className={`bg-muted border-b border-border ${className}`}>{children}</thead>;
}

interface TableBodyProps {
  children: React.ReactNode;
  className?: string;
}

export function TableBody({ children, className = '' }: TableBodyProps) {
  return <tbody className={className}>{children}</tbody>;
}

interface TableRowProps {
  children: React.ReactNode;
  className?: string;
  zebra?: boolean;
}

export function TableRow({ children, className = '', zebra = true }: TableRowProps) {
  const zebraClass = zebra ? 'even:bg-muted/30' : '';
  return (
    <tr className={`hover:bg-accent/50 transition-colors border-b border-border last:border-0 ${zebraClass} ${className}`}>
      {children}
    </tr>
  );
}

interface TableHeadProps {
  children: React.ReactNode;
  className?: string;
}

export function TableHead({ children, className = '' }: TableHeadProps) {
  return (
    <th className={`px-4 py-3 text-left text-muted-foreground ${className}`}>
      {children}
    </th>
  );
}

interface TableCellProps {
  children: React.ReactNode;
  className?: string;
}

export function TableCell({ children, className = '' }: TableCellProps) {
  return <td className={`px-4 py-3 ${className}`}>{children}</td>;
}
