import React from 'react';
import { Backup } from '../types';
import { Calendar, HardDrive, CheckCircle, XCircle, AlertCircle } from 'lucide-react';

interface BackupsListProps {
  backups: Backup[];
  onBackupClick?: (backup: Backup) => void;
  loading?: boolean;
}

export const BackupsList: React.FC<BackupsListProps> = ({ backups, onBackupClick, loading }) => {
  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString();
  };

  const formatSize = (bytes?: number) => {
    if (bytes == null) return 'N/A';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = bytes;
    let unitIndex = 0;
    
    while (size >= 1024 && unitIndex < units.length - 1) {
      size /= 1024;
      unitIndex++;
    }
    
    return `${size.toFixed(2)} ${units[unitIndex]}`;
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Success':
        return <CheckCircle className="text-green-500" size={20} />;
      case 'Failed':
        return <XCircle className="text-red-500" size={20} />;
      case 'Partial':
        return <AlertCircle className="text-yellow-500" size={20} />;
      default:
        return <HardDrive className="text-gray-400" size={20} />;
    }
  };

  if (loading) {
    return (
      <div className="flex justify-center items-center p-12">
        <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-blue-600 dark:border-blue-400"></div>
      </div>
    );
  }

  if (backups.length === 0) {
    return (
      <div className="bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 p-12 text-center">
        <div className="w-16 h-16 bg-slate-100 dark:bg-slate-700 rounded-full flex items-center justify-center mx-auto mb-4">
          <HardDrive className="text-slate-400" size={32} />
        </div>
        <h3 className="text-lg font-semibold text-slate-900 dark:text-white mb-2">No backups found</h3>
        <p className="text-sm text-slate-600 dark:text-slate-400">No backup snapshots are available for this device yet.</p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {backups.map((backup) => (
        <div
          key={backup.id}
          className={`bg-white dark:bg-slate-800 rounded-lg shadow-sm border border-slate-200 dark:border-slate-700 p-5 ${
            onBackupClick ? 'cursor-pointer hover:shadow-md hover:border-slate-300 dark:hover:border-slate-600 transition-all' : ''
          }`}
          onClick={() => onBackupClick?.(backup)}
        >
          <div className="flex items-start justify-between">
            <div className="flex items-start space-x-4 flex-1">
              <div className="mt-0.5">
                {getStatusIcon(backup.status)}
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-2">
                  <h3 className="font-semibold text-slate-900 dark:text-white">
                    {backup.deviceName}
                    {backup.shareName && <span className="text-slate-500 dark:text-slate-400"> / {backup.shareName}</span>}
                  </h3>
                  <span className={`px-2 py-0.5 text-xs font-medium rounded-full ${
                    backup.status === 'Success' 
                      ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                      : backup.status === 'Failed'
                      ? 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
                      : 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400'
                  }`}>
                    {backup.status}
                  </span>
                </div>
                
                <div className="flex items-center text-sm text-slate-600 dark:text-slate-400 mb-3">
                  <Calendar size={14} className="mr-1.5" />
                  {formatDate(backup.timestamp)}
                </div>

                <div className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span className="text-slate-500 dark:text-slate-400">Files:</span>{' '}
                    <span className="font-medium text-slate-900 dark:text-white">
                      {backup.filesNew || 0} new, {backup.filesChanged || 0} changed
                    </span>
                  </div>
                  <div>
                    <span className="text-slate-500 dark:text-slate-400">Data:</span>{' '}
                    <span className="font-medium text-slate-900 dark:text-white">{formatSize(backup.dataAdded)}</span>
                  </div>
                </div>

                {backup.errorMessage && (
                  <div className="mt-3 text-sm text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-900/20 p-2.5 rounded border border-red-200 dark:border-red-800">
                    {backup.errorMessage}
                  </div>
                )}
              </div>
            </div>
            
            <div className="text-right text-sm ml-4">
              <div className="text-slate-500 dark:text-slate-400 font-mono text-xs bg-slate-100 dark:bg-slate-700/50 px-2 py-1 rounded">
                {backup.id}
              </div>
              {backup.duration && (
                <div className="text-slate-400 dark:text-slate-500 mt-2 text-xs">
                  {backup.duration}
                </div>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
};
