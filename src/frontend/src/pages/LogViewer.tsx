import { useQuery } from '@tanstack/react-query';
import { useState, useEffect } from 'react';
import React from 'react';
import { Search, Filter, RefreshCw } from 'lucide-react';
import { LogEntry, LogQueryParameters } from '../types/Log';
import { apiClient } from '../services/api';

export default function LogViewer() {
  const [search, setSearch] = useState('');
  const [level, setLevel] = useState<string>('');
  const [limit, setLimit] = useState(100);
  const [autoRefresh, setAutoRefresh] = useState(false);

  const queryParams: LogQueryParameters = {
    search: search || undefined,
    level: level || undefined,
    limit,
  };

  const { data: logs, isLoading, error, refetch } = useQuery<LogEntry[]>({
    queryKey: ['logs', queryParams],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (queryParams.search) params.append('search', queryParams.search);
      if (queryParams.level) params.append('level', queryParams.level);
      if (queryParams.limit) params.append('limit', queryParams.limit.toString());
      
      const response = await apiClient.get(`/api/logs?${params.toString()}`);
      return response.data;
    },
  });

  // Auto-refresh every 5 seconds if enabled
  useEffect(() => {
    if (!autoRefresh) return;
    
    const interval = setInterval(() => {
      refetch();
    }, 5000);
    
    return () => clearInterval(interval);
  }, [autoRefresh, refetch]);

  const getLevelColor = (logLevel: string): string => {
    switch (logLevel.toUpperCase()) {
      case 'ERR':
      case 'ERROR':
        return 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30';
      case 'WRN':
      case 'WARNING':
        return 'text-yellow-600 dark:text-yellow-400 bg-yellow-50 dark:bg-yellow-950/30';
      case 'INF':
      case 'INFO':
        return 'text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/30';
      case 'DBG':
      case 'DEBUG':
        return 'text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/30';
      default:
        return 'text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/30';
    }
  };

  const highlightMessage = (message: string): JSX.Element => {
    // Regex patterns for highlighting (split patterns use /g, test patterns don't)
    const urlPattern = /(https?:\/\/[^\s]+|\/[^\s]*)/g;
    const httpMethodPattern = /\b(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\b/g;
    const statusCodePattern = /\b(200|201|204|400|401|403|404|500|502|503)\b/g;
    const numberPattern = /\b(\d+(?:\.\d+)?(?:ms|s|KB|MB|GB)?)\b/g;
    
    let parts: (string | JSX.Element)[] = [message];
    
    // Highlight URLs and paths
    parts = parts.flatMap((part, idx) => {
      if (typeof part !== 'string') return part;
      const segments = part.split(urlPattern);
      return segments.map((segment, i) => {
        if (/https?:\/\/[^\s]+|\/[^\s]*/.test(segment)) {
          return <span key={`url-${idx}-${i}`} className="text-cyan-600 dark:text-cyan-400">{segment}</span>;
        }
        return segment;
      });
    });
    
    // Highlight HTTP methods
    parts = parts.flatMap((part, idx) => {
      if (typeof part !== 'string') return part;
      const segments = part.split(httpMethodPattern);
      return segments.map((segment, i) => {
        if (/\b(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\b/.test(segment)) {
          return <span key={`method-${idx}-${i}`} className="text-green-600 dark:text-green-400 font-semibold">{segment}</span>;
        }
        return segment;
      });
    });
    
    // Highlight status codes
    parts = parts.flatMap((part, idx) => {
      if (typeof part !== 'string') return part;
      const segments = part.split(statusCodePattern);
      return segments.map((segment, i) => {
        if (/\b(200|201|204|400|401|403|404|500|502|503)\b/.test(segment)) {
          const code = parseInt(segment);
          const colorClass = code >= 200 && code < 300 
            ? 'text-green-600 dark:text-green-400' 
            : code >= 400 
            ? 'text-red-600 dark:text-red-400' 
            : 'text-blue-600 dark:text-blue-400';
          return <span key={`status-${idx}-${i}`} className={`${colorClass} font-semibold`}>{segment}</span>;
        }
        return segment;
      });
    });
    
    // Highlight numbers and measurements
    parts = parts.flatMap((part, idx) => {
      if (typeof part !== 'string') return part;
      const segments = part.split(numberPattern);
      return segments.map((segment, i) => {
        if (/\b(\d+(?:\.\d+)?(?:ms|s|KB|MB|GB)?)\b/.test(segment)) {
          return <span key={`num-${idx}-${i}`} className="text-purple-600 dark:text-purple-400">{segment}</span>;
        }
        return segment;
      });
    });
    
    return <>{parts}</>;
  };

  const formatTimestamp = (timestamp: string): string => {
    return new Date(timestamp).toLocaleString();
  };

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-2">Application Logs</h1>
        <p className="text-gray-600 dark:text-gray-400">View and search application logs</p>
      </div>

      {/* Filters */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 mb-4">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {/* Search */}
          <div className="md:col-span-2">
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Search
            </label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400 dark:text-gray-500 w-4 h-4" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search in messages..."
                className="w-full pl-10 pr-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {/* Level Filter */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              <Filter className="inline w-4 h-4 mr-1" />
              Level
            </label>
            <select
              value={level}
              onChange={(e) => setLevel(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">All Levels</option>
              <option value="INF">Info</option>
              <option value="WRN">Warning</option>
              <option value="ERR">Error</option>
              <option value="DBG">Debug</option>
            </select>
          </div>

          {/* Limit */}
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Limit
            </label>
            <select
              value={limit}
              onChange={(e) => setLimit(Number(e.target.value))}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value={50}>50</option>
              <option value={100}>100</option>
              <option value={250}>250</option>
              <option value={500}>500</option>
              <option value={1000}>1000</option>
            </select>
          </div>
        </div>

        {/* Auto-refresh toggle */}
        <div className="mt-4 flex items-center justify-between">
          <label className="flex items-center cursor-pointer">
            <input
              type="checkbox"
              checked={autoRefresh}
              onChange={(e) => setAutoRefresh(e.target.checked)}
              className="mr-2 h-4 w-4 rounded border-gray-300 dark:border-gray-600 text-blue-600 focus:ring-blue-500"
            />
            <RefreshCw className={`w-4 h-4 mr-1 ${autoRefresh ? 'text-blue-600 dark:text-blue-400 animate-spin' : 'text-gray-600 dark:text-gray-400'}`} />
            <span className="text-sm text-gray-700 dark:text-gray-300">Auto-refresh (5s)</span>
          </label>

          <button
            onClick={() => refetch()}
            className="px-4 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            Refresh Now
          </button>
        </div>
      </div>

      {/* Log Entries */}
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
        {isLoading && (
          <div className="p-8 text-center text-gray-500 dark:text-gray-400">
            Loading logs...
          </div>
        )}

        {error && (
          <div className="p-8 text-center text-red-600 dark:text-red-400">
            Error loading logs: {error instanceof Error ? error.message : 'Unknown error'}
          </div>
        )}

        {!isLoading && !error && logs && logs.length === 0 && (
          <div className="p-8 text-center text-gray-500 dark:text-gray-400">
            No logs found matching the current filters
          </div>
        )}

        {!isLoading && !error && logs && logs.length > 0 && (
          <div className="divide-y divide-gray-100 dark:divide-gray-700/50 max-h-[calc(100vh-300px)] overflow-y-auto font-mono text-[13px]">
            {logs.map((log, index) => (
              <div key={index} className="px-2 py-1 hover:bg-gray-50 dark:hover:bg-gray-700/30 flex items-start gap-2 leading-tight">
                {/* Timestamp */}
                <span className="text-gray-500 dark:text-gray-500 whitespace-nowrap flex-shrink-0">
                  {formatTimestamp(log.timestamp)}
                </span>

                {/* Level Badge */}
                <span className={`px-1.5 py-0 rounded text-[10px] font-semibold uppercase ${getLevelColor(log.level)} flex-shrink-0 leading-tight`}>
                  {log.level.substring(0, 3)}
                </span>

                {/* Message */}
                <span className="text-gray-900 dark:text-gray-100 break-all flex-1 min-w-0 text-left">
                  {highlightMessage(log.message)}
                  {log.sourceContext && (
                    <span className="text-gray-500 dark:text-gray-500 ml-2">
                      [{log.sourceContext}]
                    </span>
                  )}
                  {log.exception && (
                    <details className="ml-2 inline">
                      <summary className="text-red-600 dark:text-red-400 cursor-pointer hover:underline inline">
                        [exception]
                      </summary>
                      <pre className="mt-0.5 p-1 bg-red-50 dark:bg-red-950/30 text-red-800 dark:text-red-300 text-[11px] rounded overflow-x-auto block">
                        {log.exception}
                      </pre>
                    </details>
                  )}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Summary */}
      {!isLoading && logs && (
        <div className="mt-4 text-sm text-gray-600 dark:text-gray-400 text-center">
          Showing {logs.length} log {logs.length === 1 ? 'entry' : 'entries'}
          {logs.length >= limit && ` (limited to ${limit})`}
        </div>
      )}
    </div>
  );
}
