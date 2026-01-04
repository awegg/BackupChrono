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
      <div className="flex justify-center items-center p-8">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  if (backups.length === 0) {
    return (
      <div className="text-center p-8 text-gray-500">
        <HardDrive className="mx-auto mb-4 text-gray-400" size={48} />
        <p>No backups found</p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {backups.map((backup) => (
        <div
          key={backup.id}
          className={`bg-white rounded-lg shadow p-4 border border-gray-200 ${
            onBackupClick ? 'cursor-pointer hover:shadow-md transition-shadow' : ''
          }`}
          onClick={() => onBackupClick?.(backup)}
        >
          <div className="flex items-start justify-between">
            <div className="flex items-start space-x-3 flex-1">
              <div className="mt-1">
                {getStatusIcon(backup.status)}
              </div>
              <div className="flex-1">
                <div className="flex items-center space-x-2">
                  <h3 className="font-semibold text-gray-900">
                    {backup.deviceName}
                    {backup.shareName && <span className="text-gray-500"> / {backup.shareName}</span>}
                  </h3>
                  <span className={`px-2 py-1 text-xs rounded ${
                    backup.status === 'Success' 
                      ? 'bg-green-100 text-green-800'
                      : backup.status === 'Failed'
                      ? 'bg-red-100 text-red-800'
                      : 'bg-yellow-100 text-yellow-800'
                  }`}>
                    {backup.status}
                  </span>
                </div>
                
                <div className="flex items-center text-sm text-gray-500 mt-1">
                  <Calendar size={14} className="mr-1" />
                  {formatDate(backup.timestamp)}
                </div>

                <div className="grid grid-cols-2 gap-4 mt-2 text-sm">
                  <div>
                    <span className="text-gray-500">Files:</span>{' '}
                    <span className="font-medium">
                      {backup.filesNew || 0} new, {backup.filesChanged || 0} changed
                    </span>
                  </div>
                  <div>
                    <span className="text-gray-500">Data:</span>{' '}
                    <span className="font-medium">{formatSize(backup.dataAdded)}</span>
                  </div>
                </div>

                {backup.errorMessage && (
                  <div className="mt-2 text-sm text-red-600 bg-red-50 p-2 rounded">
                    {backup.errorMessage}
                  </div>
                )}
              </div>
            </div>
            
            <div className="text-right text-sm">
              <div className="text-gray-500 font-mono text-xs">
                {backup.id}
              </div>
              {backup.duration && (
                <div className="text-gray-400 mt-1">
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
