import { useState, useEffect } from 'react';
import { healthService } from '../services/healthService';
import { HealthStatus } from '../types/health';
import { 
  Activity, 
  CheckCircle, 
  AlertTriangle, 
  XCircle, 
  RefreshCw,
  Clock,
  Package,
  ChevronDown,
  ChevronUp
} from 'lucide-react';

export function HealthStatusPanel() {
  const [health, setHealth] = useState<HealthStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isExpanded, setIsExpanded] = useState(false);
  const [expandedChecks, setExpandedChecks] = useState<Set<string>>(new Set());
  const [autoRefresh, setAutoRefresh] = useState(true);

  const loadHealth = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await healthService.getStatus();
      setHealth(data);
    } catch (err) {
      setError('Failed to load health status');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadHealth();
    
    if (autoRefresh) {
      const interval = setInterval(loadHealth, 30000); // Refresh every 30 seconds
      return () => clearInterval(interval);
    }
  }, [autoRefresh]);

  const toggleCheckExpanded = (checkName: string) => {
    setExpandedChecks(prev => {
      const newSet = new Set(prev);
      if (newSet.has(checkName)) {
        newSet.delete(checkName);
      } else {
        newSet.add(checkName);
      }
      return newSet;
    });
  };

  const getStatusIcon = (status: string) => {
    switch (status.toLowerCase()) {
      case 'healthy':
        return <CheckCircle className="w-4 h-4 text-green-500" />;
      case 'warning':
      case 'degraded':
        return <AlertTriangle className="w-4 h-4 text-yellow-500" />;
      case 'critical':
      case 'unhealthy':
        return <XCircle className="w-4 h-4 text-red-500" />;
      default:
        return <Activity className="w-4 h-4 text-gray-500" />;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status.toLowerCase()) {
      case 'healthy':
        return 'bg-green-50 border-green-200 text-green-800';
      case 'warning':
      case 'degraded':
        return 'bg-yellow-50 border-yellow-200 text-yellow-800';
      case 'critical':
      case 'unhealthy':
        return 'bg-red-50 border-red-200 text-red-800';
      default:
        return 'bg-gray-50 border-gray-200 text-gray-800';
    }
  };

  const formatUptime = (uptime: string) => {
    // uptime format from C# TimeSpan: "HH:MM:SS.ffffff"
    const parts = uptime.split(':');
    if (parts.length < 2) return uptime;
    
    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    
    if (hours > 24) {
      const days = Math.floor(hours / 24);
      const remainingHours = hours % 24;
      return `${days}d ${remainingHours}h ${minutes}m`;
    }
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    
    return `${minutes}m`;
  };

  if (loading && !health) {
    return (
      <div className="bg-white rounded-lg shadow-sm border p-3">
        <div className="flex items-center text-sm">
          <RefreshCw className="w-4 h-4 animate-spin text-blue-500 mr-2" />
          <span className="text-gray-600">Loading...</span>
        </div>
      </div>
    );
  }

  if (error && !health) {
    return (
      <div className="bg-white rounded-lg shadow-sm border p-3">
        <div className="flex items-center justify-between text-sm">
          <div className="flex items-center text-red-600">
            <XCircle className="w-4 h-4 mr-2" />
            <span>{error}</span>
          </div>
          <button
            onClick={loadHealth}
            className="text-blue-600 hover:text-blue-800 text-xs underline"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  if (!health) return null;

  // Collapsed view - just show overall status
  if (!isExpanded) {
    const criticalCount = health.checks.filter(c => c.status.toLowerCase() === 'critical').length;
    const warningCount = health.checks.filter(c => c.status.toLowerCase() === 'warning' || c.status.toLowerCase() === 'degraded').length;

    return (
      <div className="bg-white rounded-lg shadow-sm border">
        <button
          onClick={() => setIsExpanded(true)}
          className="w-full p-3 flex items-center justify-between hover:bg-gray-50 transition-colors"
        >
          <div className="flex items-center space-x-3">
            {getStatusIcon(health.status)}
            <div className="text-left">
              <div className="text-sm font-semibold">System Health: {health.status}</div>
              <div className="text-xs text-gray-500">
                {criticalCount > 0 && <span className="text-red-600">{criticalCount} critical</span>}
                {criticalCount > 0 && warningCount > 0 && <span className="mx-1">•</span>}
                {warningCount > 0 && <span className="text-yellow-600">{warningCount} warning</span>}
                {criticalCount === 0 && warningCount === 0 && <span>All checks passing</span>}
              </div>
            </div>
          </div>
          <ChevronDown className="w-4 h-4 text-gray-400" />
        </button>
      </div>
    );
  }

  // Expanded view - show full details
  return (
    <div className="bg-white rounded-lg shadow-sm border">
      <div className="p-4 border-b">
        <div className="flex items-center justify-between">
          <button
            onClick={() => setIsExpanded(false)}
            className="flex items-center space-x-2 hover:text-gray-600"
          >
            <Activity className="w-4 h-4" />
            <h3 className="text-lg font-semibold">System Health</h3>
            <ChevronUp className="w-4 h-4 text-gray-400" />
          </button>
          <div className="flex items-center space-x-3">
            <label className="flex items-center text-xs text-gray-600">
              <input
                type="checkbox"
                checked={autoRefresh}
                onChange={(e) => setAutoRefresh(e.target.checked)}
                className="mr-1"
              />
              Auto-refresh
            </label>
            <button
              onClick={loadHealth}
              disabled={loading}
              className="text-blue-600 hover:text-blue-800 disabled:opacity-50"
              title="Refresh now"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>
      </div>

      <div className="p-4 space-y-3">
        {/* Overall Status */}
        <div className={`border rounded p-3 ${getStatusColor(health.status)}`}>
          <div className="flex items-center justify-between">
            <div className="flex items-center">
              {getStatusIcon(health.status)}
              <span className="ml-2 font-semibold text-sm">
                {health.status}
              </span>
            </div>
            <div className="flex items-center space-x-3 text-xs">
              <div className="flex items-center">
                <Package className="w-3 h-3 mr-1" />
                v{health.version}
              </div>
              <div className="flex items-center">
                <Clock className="w-3 h-3 mr-1" />
                {formatUptime(health.uptime)}
              </div>
            </div>
          </div>
        </div>

        {/* Individual Checks */}
        <div className="space-y-2">
          {health.checks.map((check) => (
            <div
              key={check.name}
              className={`border rounded p-2 ${getStatusColor(check.status)}`}
            >
              <div
                className="flex items-center justify-between cursor-pointer"
                onClick={() => toggleCheckExpanded(check.name)}
              >
                <div className="flex items-center flex-1 min-w-0">
                  {getStatusIcon(check.status)}
                  <div className="ml-2 flex-1 min-w-0">
                    <div className="font-medium text-sm">{check.name}</div>
                    <div className="text-xs truncate">{check.message}</div>
                  </div>
                </div>
                {check.details && (
                  <button className="ml-2 p-1 hover:bg-black/5 rounded flex-shrink-0">
                    {expandedChecks.has(check.name) ? (
                      <ChevronUp className="w-3 h-3" />
                    ) : (
                      <ChevronDown className="w-3 h-3" />
                    )}
                  </button>
                )}
              </div>

              {/* Expanded Details */}
              {expandedChecks.has(check.name) && check.details && (
                <div className="mt-2 pt-2 border-t border-current/20">
                  <div className="text-xs font-mono bg-black/5 p-2 rounded max-h-40 overflow-auto">
                    <pre className="whitespace-pre-wrap break-words">
                      {JSON.stringify(check.details, null, 2)}
                    </pre>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>

        {/* Last Updated */}
        <div className="text-xs text-gray-500 text-right pt-2 border-t">
          Last updated: {new Date(health.timestamp).toLocaleString()}
        </div>
      </div>
    </div>
  );
}
