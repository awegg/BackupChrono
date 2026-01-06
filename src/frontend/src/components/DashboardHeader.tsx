interface DashboardHeaderProps {
  title: string;
  subtitle?: string;
  lastUpdated: string;
  isConnected?: boolean;
}

export function DashboardHeader({ title, subtitle, lastUpdated, isConnected = true }: DashboardHeaderProps) {
  return (
    <div className="flex items-center justify-between mb-6">
      <div>
        <h1 className="text-3xl font-semibold text-foreground">{title}</h1>
        {subtitle && (
          <p className="text-muted-foreground mt-1">{subtitle}</p>
        )}
      </div>
      
      <div className={`flex items-center gap-2 px-3 py-1.5 rounded-lg ${
        isConnected ? 'bg-status-success-bg' : 'bg-status-error-bg'
      }`}>
        <div className={`w-2 h-2 rounded-full ${
          isConnected 
            ? 'bg-status-success animate-pulse' 
            : 'bg-status-error'
        }`} />
        <span className={`text-sm font-medium ${
          isConnected ? 'text-status-success-fg' : 'text-status-error-fg'
        }`}>
          {isConnected ? 'Live' : 'Disconnected'} • Updated {lastUpdated}
        </span>
      </div>
    </div>
  );
}
