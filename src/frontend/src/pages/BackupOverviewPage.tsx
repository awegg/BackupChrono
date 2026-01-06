import React from 'react';
import { RefreshCw, Search, Clock } from 'lucide-react';
import { SummaryCard } from '../components/SummaryCard';
import { DeviceShareTable } from '../components/DeviceShareTable';
import { mockDashboardData, BackupStatus } from '../data/mockBackupData';
import { StatusBadge } from '../components/StatusBadge';

type SortField = 'name' | 'lastBackup' | 'status' | 'size' | 'files';
type SortDirection = 'asc' | 'desc';

// Helper function to check if a backup is stale (>2 days old or never backed up)
const isBackupStale = (lastBackup: Date | null): boolean => {
  if (!lastBackup) return true; // Never backed up
  const twoDaysAgo = new Date();
  twoDaysAgo.setDate(twoDaysAgo.getDate() - 2);
  return lastBackup < twoDaysAgo;
};

export const BackupOverviewPage: React.FC = () => {
  const [lastUpdated, setLastUpdated] = React.useState(new Date());
  const [searchQuery, setSearchQuery] = React.useState('');
  const [statusFilter, setStatusFilter] = React.useState<BackupStatus | 'all' | 'stale'>('all');
  const [sortField, setSortField] = React.useState<SortField>('name');
  const [sortDirection, setSortDirection] = React.useState<SortDirection>('asc');
  const [expandedDevices, setExpandedDevices] = React.useState<Set<string>>(
    new Set(mockDashboardData.devices.map(d => d.id))
  );

  const handleRefresh = () => {
    setLastUpdated(new Date());
    console.log('Refreshing dashboard data...');
    // TODO: Fetch real data from API
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const handleStatusFilterClick = (status: BackupStatus | 'all' | 'stale') => {
    setStatusFilter(statusFilter === status ? 'all' : status);
  };

  const handleCardClick = (filter: 'attention' | 'all' | 'failures') => {
    if (filter === 'attention') {
      setStatusFilter(statusFilter === 'Warning' ? 'all' : 'Warning');
    } else if (filter === 'failures') {
      setStatusFilter(statusFilter === 'Failed' ? 'all' : 'Failed');
    } else {
      setStatusFilter('all');
    }
  };

  const handleToggleDevice = (deviceId: string) => {
    setExpandedDevices(prev => {
      const next = new Set(prev);
      if (next.has(deviceId)) {
        next.delete(deviceId);
      } else {
        next.add(deviceId);
      }
      return next;
    });
  };

  // Filter and sort devices
  const filteredDevices = React.useMemo(() => {
    let filtered = mockDashboardData.devices.map(device => {
      // Filter shares within each device
      const filteredShares = device.shares.filter(share => {
        const matchesSearch = searchQuery === '' || 
          share.path.toLowerCase().includes(searchQuery.toLowerCase()) ||
          device.name.toLowerCase().includes(searchQuery.toLowerCase());
        const matchesStatus = statusFilter === 'all' || 
                             share.status === statusFilter ||
                             (statusFilter === 'stale' && isBackupStale(share.lastBackup));
        return matchesSearch && matchesStatus;
      });

      return { ...device, shares: filteredShares };
    }).filter(device => {
      // Keep device if it has matching shares or if the device itself matches search
      const deviceMatchesSearch = searchQuery === '' || 
        device.name.toLowerCase().includes(searchQuery.toLowerCase());
      return device.shares.length > 0 || (deviceMatchesSearch && statusFilter === 'all');
    });

    // Sort shares within each device
    filtered = filtered.map(device => {
      const sortedShares = [...device.shares].sort((a, b) => {
        let comparison = 0;
        switch (sortField) {
          case 'name':
            comparison = a.path.localeCompare(b.path);
            break;
          case 'lastBackup':
            const aTime = a.lastBackup?.getTime() ?? 0;
            const bTime = b.lastBackup?.getTime() ?? 0;
            comparison = aTime - bTime;
            break;
          case 'status':
            comparison = a.status.localeCompare(b.status);
            break;
          case 'size':
            comparison = a.sizeGB - b.sizeGB;
            break;
          case 'files':
            comparison = a.fileCount - b.fileCount;
            break;
        }
        return sortDirection === 'asc' ? comparison : -comparison;
      });
      return { ...device, shares: sortedShares };
    });

    return filtered;
  }, [searchQuery, statusFilter, sortField, sortDirection]);

  const formattedLastUpdated = lastUpdated.toLocaleString('en-US', {
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });

  const hasNoDevices = mockDashboardData.devices.length === 0;
  const hasNoResults = filteredDevices.length === 0 && !hasNoDevices;

  const statusCounts = React.useMemo(() => {
    const counts: Record<BackupStatus, number> = {
      Success: 0,
      Failed: 0,
      Running: 0,
      Warning: 0,
      Disabled: 0,
      Partial: 0,
    };
    mockDashboardData.devices.forEach(device => {
      device.shares.forEach(share => {
        counts[share.status]++;
      });
    });
    return counts;
  }, []);

  // Calculate stale backups count (>2 days or never backed up)
  const staleCount = React.useMemo(() => {
    let count = 0;
    mockDashboardData.devices.forEach(device => {
      device.shares.forEach(share => {
        if (isBackupStale(share.lastBackup)) {
          count++;
        }
      });
    });
    return count;
  }, []);

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900">
      {/* Header */}
      <div className="bg-white dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 px-8 py-4">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
              Backup Overview
            </h1>
            <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
              Operational dashboard showing all devices and shares
            </p>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-sm text-slate-600 dark:text-slate-400">
              Last updated: {formattedLastUpdated}
            </span>
            <button
              onClick={handleRefresh}
              className="p-2 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
              aria-label="Refresh"
            >
              <RefreshCw className="w-4 h-4 text-slate-600 dark:text-slate-400" />
            </button>
          </div>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Search devices or shares..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-600 rounded-lg text-sm text-slate-900 dark:text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {/* Main Content */}
      <div className="px-8 py-6">
        {/* Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
          <SummaryCard
            title="Devices Needing Attention"
            value={mockDashboardData.devicesNeedingAttention}
            subtitle="requiring action"
            variant="warning"
            onClick={() => handleCardClick('attention')}
          />
          <SummaryCard
            title="Total Protected Data"
            value={`${mockDashboardData.totalProtectedDataTB.toFixed(1)} TB`}
            subtitle={`across ${mockDashboardData.devices.length} devices`}
            variant="info"
            onClick={() => handleCardClick('all')}
          />
          <SummaryCard
            title="Recent Failures"
            value={mockDashboardData.recentFailures}
            subtitle="in last 24 hours"
            variant="error"
            onClick={() => handleCardClick('failures')}
          />
        </div>

        {/* Status Filter Badges */}
        <div className="flex items-center gap-2 mb-4 flex-wrap">
          <span className="text-xs font-medium text-slate-600 dark:text-slate-400 uppercase tracking-wider">
            Filter:
          </span>
          <button
            onClick={() => handleStatusFilterClick('all')}
            className={`px-3 py-1 rounded-full text-xs font-medium transition-colors ${
              statusFilter === 'all'
                ? 'bg-slate-200 dark:bg-slate-700 text-slate-900 dark:text-white'
                : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-200 dark:hover:bg-slate-700'
            }`}
          >
            All ({mockDashboardData.devices.reduce((acc, d) => acc + d.shares.length, 0)})
          </button>
          {(['Success', 'Failed', 'Running', 'Warning', 'Partial', 'Disabled'] as BackupStatus[]).map(status => (
            <button
              key={status}
              onClick={() => handleStatusFilterClick(status)}
              className={`transition-opacity ${
                statusFilter === status ? 'opacity-100' : 'opacity-50 hover:opacity-75'
              }`}
            >
              <div className="flex items-center gap-1">
                <StatusBadge status={status} />
                <span className="text-xs text-slate-600 dark:text-slate-400">
                  ({statusCounts[status]})
                </span>
              </div>
            </button>
          ))}
          <button
            onClick={() => handleStatusFilterClick('stale')}
            className={`px-3 py-1 rounded-full text-xs font-medium transition-all flex items-center gap-1.5 ${
              statusFilter === 'stale'
                ? 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-400 border-2 border-orange-500'
                : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:bg-orange-50 dark:hover:bg-orange-900/20 border-2 border-transparent'
            }`}
            title="Backups older than 2 days or never backed up"
          >
            <Clock className="w-3.5 h-3.5" />
            <span>Stale ({staleCount})</span>
          </button>
        </div>

        {/* Device/Share Table or Empty State */}
        {hasNoDevices ? (
          <div className="bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 p-12 text-center">
            <div className="max-w-md mx-auto">
              <div className="w-16 h-16 bg-slate-100 dark:bg-slate-700 rounded-full flex items-center justify-center mx-auto mb-4">
                <svg className="w-8 h-8 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 19a2 2 0 01-2-2V7a2 2 0 012-2h4l2 2h4a2 2 0 012 2v1M5 19h14a2 2 0 002-2v-5a2 2 0 00-2-2H9a2 2 0 00-2 2v5a2 2 0 01-2 2z" />
                </svg>
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white mb-2">
                No devices configured
              </h3>
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-6">
                Get started by adding your first device to begin backing up your data.
              </p>
              <button className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-sm font-medium transition-colors">
                Add Your First Device
              </button>
            </div>
          </div>
        ) : hasNoResults ? (
          <div className="bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 p-12 text-center">
            <div className="max-w-md mx-auto">
              <div className="w-16 h-16 bg-slate-100 dark:bg-slate-700 rounded-full flex items-center justify-center mx-auto mb-4">
                <Search className="w-8 h-8 text-slate-400" />
              </div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-white mb-2">
                No results found
              </h3>
              <p className="text-sm text-slate-600 dark:text-slate-400 mb-4">
                No devices or shares match your current filters.
              </p>
              <button
                onClick={() => {
                  setSearchQuery('');
                  setStatusFilter('all');
                }}
                className="px-4 py-2 bg-slate-100 dark:bg-slate-700 hover:bg-slate-200 dark:hover:bg-slate-600 text-slate-900 dark:text-white rounded-lg text-sm font-medium transition-colors"
              >
                Clear Filters
              </button>
            </div>
          </div>
        ) : (
          <DeviceShareTable
            devices={filteredDevices}
            sortField={sortField}
            sortDirection={sortDirection}
            onSort={handleSort}
            expandedDevices={expandedDevices}
            onToggleDevice={handleToggleDevice}
          />
        )}
      </div>
    </div>
  );
};
