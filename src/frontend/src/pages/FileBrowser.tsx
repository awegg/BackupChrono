import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Backup, FileEntry } from '../types';
import { FileBrowser } from '../components/FileBrowser';
import { ChevronLeft, HardDrive, RotateCcw, X } from 'lucide-react';
import { apiClient } from '../services/api';

export const FileBrowserPage: React.FC = () => {
  const { deviceId, backupId } = useParams<{ deviceId: string; backupId: string }>();
  const navigate = useNavigate();
  
  const [backup, setBackup] = useState<Backup | null>(null);
  const [files, setFiles] = useState<FileEntry[]>([]);
  const [currentPath, setCurrentPath] = useState('/');
  const [loading, setLoading] = useState(false);
  const [filesLoading, setFilesLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showRestoreDialog, setShowRestoreDialog] = useState(false);
  const [restoreTargetPath, setRestoreTargetPath] = useState('');
  const [restoreCurrentFolderOnly, setRestoreCurrentFolderOnly] = useState(false);
  const [restoring, setRestoring] = useState(false);
  const [restoreSuccess, setRestoreSuccess] = useState<string | null>(null);

  useEffect(() => {
    if (!deviceId || !backupId) return;
    
    const loadBackupAndFiles = async () => {
      setLoading(true);
      setError(null);
      
      try {
        // Load all backups to find the one we need
        const backupsResponse = await apiClient.get(`/api/devices/${deviceId}/backups`);
        const foundBackup = backupsResponse.data.find((b: Backup) => b.id === backupId);
        
        if (!foundBackup) {
          setError('Backup not found');
          setLoading(false);
          return;
        }
        
        setBackup(foundBackup);
        
        // Load files for this backup
        await loadFiles(foundBackup.id, '/', foundBackup.deviceId, foundBackup.shareId || '');
      } catch (err) {
        console.error('Error loading backup:', err);
        setError('Failed to load backup');
      } finally {
        setLoading(false);
      }
    };
    
    loadBackupAndFiles();
  }, [deviceId, backupId]);

  const loadFiles = async (backupId: string, path: string, deviceId: string, shareId: string) => {
    setFilesLoading(true);
    setError(null);
    
    try {
      const response = await apiClient.get(
        `/api/backups/${backupId}/files`,
        {
          params: { 
            path,
            deviceId,
            shareId
          }
        }
      );
      setFiles(response.data);
      setCurrentPath(path);
    } catch (err) {
      console.error('Error loading files:', err);
      setError('Failed to load files');
    } finally {
      setFilesLoading(false);
    }
  };

  const handleNavigate = (path: string) => {
    if (backup) {
      loadFiles(backup.id, path, backup.deviceId, backup.shareId || '');
    }
  };

  const handleDownload = async (file: FileEntry) => {
    if (!backup) return;
    
    try {
      const response = await apiClient.get(
        `/api/backups/${backup.id}/download`,
        {
          params: { 
            filePath: file.path,
            deviceId: backup.deviceId,
            shareId: backup.shareId || ''
          },
          responseType: 'blob'
        }
      );
      
      // Create a download link
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', file.name);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Error downloading file:', err);
      setError('Failed to download file');
    }
  };

  const handleBack = () => {
    navigate(`/devices/${deviceId}/backups`);
  };

  const handleRestore = async () => {
    if (!backup || !restoreTargetPath.trim()) {
      setError('Please enter a target path');
      return;
    }

    setRestoring(true);
    setError(null);
    setRestoreSuccess(null);

    try {
      const payload: any = {
        targetPath: restoreTargetPath.trim()
      };

      // If restoring current folder only, add it to include paths
      if (restoreCurrentFolderOnly && currentPath !== '/') {
        payload.includePaths = [currentPath];
      }

      const response = await apiClient.post(
        `/api/backups/${backup.id}/restore?deviceId=${backup.deviceId}&shareId=${backup.shareId || ''}`,
        payload
      );

      setRestoreSuccess(
        `Restore completed successfully to ${response.data.targetPath}`
      );
      setShowRestoreDialog(false);
      setRestoreTargetPath('');
      setRestoreCurrentFolderOnly(false);
    } catch (err: any) {
      console.error('Error restoring backup:', err);
      const errorMsg = err.response?.data?.detail || err.response?.data?.error || 'Failed to restore backup';
      setError(errorMsg);
    } finally {
      setRestoring(false);
    }
  };

  const openRestoreDialog = () => {
    setShowRestoreDialog(true);
    setError(null);
    setRestoreSuccess(null);
  };

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex justify-center items-center p-8">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
        </div>
      </div>
    );
  }

  if (!backup) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center p-8 text-red-600">
          {error || 'Backup not found'}
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6">
        <button
          onClick={handleBack}
          className="inline-flex items-center text-sm text-gray-600 hover:text-gray-900 mb-4"
        >
          <ChevronLeft size={16} className="mr-1" />
          Back to Backups
        </button>
        
        <div className="flex items-center space-x-3">
          <HardDrive className="text-gray-400" size={32} />
          <div>
            <h1 className="text-2xl font-bold text-gray-900">
              Browse Files
            </h1>
            <p className="text-sm text-gray-500 mt-1">
              Backup from {new Date(backup.timestamp).toLocaleString()}
            </p>
          </div>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="mb-6 bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      {/* Success Display */}
      {restoreSuccess && (
        <div className="mb-6 bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded" role="status">
          {restoreSuccess}
        </div>
      )}

      {/* Content */}
      <div>
        <div className="mb-4 flex justify-end">
          <button
            onClick={openRestoreDialog}
            className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
          >
            <RotateCcw size={18} className="mr-2" />
            Restore Files
          </button>
        </div>
        
        <FileBrowser
          backupId={backup.id}
          files={files}
          currentPath={currentPath}
          onNavigate={handleNavigate}
          onDownload={handleDownload}
          loading={filesLoading}
        />
      </div>

      {/* Restore Dialog */}
      {showRestoreDialog && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full mx-4">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-lg font-semibold text-gray-900">Restore Files</h3>
              <button
                onClick={() => setShowRestoreDialog(false)}
                className="text-gray-400 hover:text-gray-600"
                disabled={restoring}
              >
                <X size={20} />
              </button>
            </div>

            <div className="p-4 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Target Path (on server)
                </label>
                <input
                  type="text"
                  value={restoreTargetPath}
                  onChange={(e) => setRestoreTargetPath(e.target.value)}
                  placeholder="/tmp/restored-files"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                  disabled={restoring}
                />
                <p className="mt-1 text-xs text-gray-500">
                  Files will be restored to this path on the server where BackupChrono is running
                </p>
              </div>

              {currentPath !== '/' && (
                <div className="flex items-start">
                  <input
                    type="checkbox"
                    id="currentFolderOnly"
                    checked={restoreCurrentFolderOnly}
                    onChange={(e) => setRestoreCurrentFolderOnly(e.target.checked)}
                    className="mt-1 h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                    disabled={restoring}
                  />
                  <label htmlFor="currentFolderOnly" className="ml-2 text-sm text-gray-700">
                    Restore current folder only ({currentPath})
                  </label>
                </div>
              )}

              <div className="bg-blue-50 border border-blue-200 rounded-md p-3">
                <p className="text-sm text-blue-800">
                  {restoreCurrentFolderOnly && currentPath !== '/'
                    ? `This will restore files from ${currentPath} to ${restoreTargetPath || '(target path)'}`
                    : `This will restore the entire backup to ${restoreTargetPath || '(target path)'}`}
                </p>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 p-4 border-t bg-gray-50">
              <button
                onClick={() => setShowRestoreDialog(false)}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
                disabled={restoring}
              >
                Cancel
              </button>
              <button
                onClick={handleRestore}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
                disabled={restoring || !restoreTargetPath.trim()}
              >
                {restoring ? 'Restoring...' : 'Restore'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
