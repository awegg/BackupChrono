import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Folder, File, Download, ChevronRight, Home, ArrowLeft } from 'lucide-react';
import { backupService } from '../services/backupService';
import { FileEntry } from '../types';

export function FileBrowserPage() {
  const { backupId } = useParams<{ backupId: string }>();
  const navigate = useNavigate();
  const [files, setFiles] = useState<FileEntry[]>([]);
  const [currentPath, setCurrentPath] = useState('/');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deviceId, setDeviceId] = useState<string>('');
  const [shareId, setShareId] = useState<string>('');

  // Get deviceId and shareId from URL params or state
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const device = params.get('deviceId') || '';
    const share = params.get('shareId') || '';
    setDeviceId(device);
    setShareId(share);
  }, []);

  const loadFiles = useCallback(async (path: string) => {
    if (!backupId || !deviceId || !shareId) return;
    
    setLoading(true);
    setError(null);
    try {
      const fileList = await backupService.browseBackupFiles(backupId, deviceId, shareId, path);
      setFiles(fileList);
    } catch (err) {
      console.error('Failed to load files:', err);
      setError(err instanceof Error ? err.message : 'Failed to load files');
    } finally {
      setLoading(false);
    }
  }, [backupId, deviceId, shareId]);

  useEffect(() => {
    if (backupId && deviceId && shareId) {
      loadFiles(currentPath);
    }
  }, [backupId, deviceId, shareId, currentPath, loadFiles]);

  const navigateToFolder = (folderPath: string) => {
    setCurrentPath(folderPath);
  };

  const navigateUp = () => {
    if (currentPath === '/') return;
    const parts = currentPath.split('/').filter(Boolean);
    parts.pop();
    setCurrentPath(parts.length > 0 ? '/' + parts.join('/') : '/');
  };

  const downloadFile = (file: FileEntry) => {
    if (!backupId || !deviceId || !shareId) return;
    const downloadUrl = backupService.getDownloadUrl(backupId, deviceId, shareId, file.path);
    window.open(downloadUrl, '_blank');
  };

  const renderBreadcrumb = () => {
    if (currentPath === '/') {
      return (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Home className="w-4 h-4" />
          <span>root</span>
        </div>
      );
    }

    const parts = currentPath.split('/').filter(Boolean);
    return (
      <div className="flex items-center gap-2 text-sm">
        <button
          onClick={() => setCurrentPath('/')}
          className="flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors"
        >
          <Home className="w-4 h-4" />
          <span>root</span>
        </button>
        {parts.map((part, index) => {
          const path = '/' + parts.slice(0, index + 1).join('/');
          const isLast = index === parts.length - 1;
          return (
            <div key={path} className="flex items-center gap-2">
              <ChevronRight className="w-4 h-4 text-muted-foreground" />
              {isLast ? (
                <span className="font-medium text-foreground">{part}</span>
              ) : (
                <button
                  onClick={() => setCurrentPath(path)}
                  className="text-muted-foreground hover:text-foreground transition-colors"
                >
                  {part}
                </button>
              )}
            </div>
          );
        })}
      </div>
    );
  };

  const formatFileSize = (bytes?: number) => {
    if (!bytes || bytes === 0) return '-';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return '-';
    return new Date(dateString).toLocaleString();
  };

  if (!backupId || !deviceId || !shareId) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="text-center">
          <p className="text-muted-foreground">Missing backup information</p>
          <button
            onClick={() => navigate('/dashboard')}
            className="mt-4 px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
          >
            Return to Dashboard
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-foreground">Backup File Browser</h1>
          <p className="text-muted-foreground mt-1">Browse and restore files from backup snapshots</p>
        </div>
        <button
          onClick={() => navigate(-1)}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-foreground bg-card hover:bg-muted border border-border rounded-md transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Back
        </button>
      </div>

      {/* Breadcrumb and Navigation */}
      <div className="bg-card rounded-lg shadow-sm border border-border p-4">
        <div className="flex items-center justify-between">
          {renderBreadcrumb()}
          {currentPath !== '/' && (
            <button
              onClick={navigateUp}
              className="flex items-center gap-2 px-3 py-1.5 text-sm font-medium text-foreground bg-secondary hover:bg-secondary-hover rounded-md transition-colors"
            >
              <ArrowLeft className="w-3.5 h-3.5" />
              Up
            </button>
          )}
        </div>
      </div>

      {/* File List */}
      <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-muted-foreground">
            Loading files...
          </div>
        ) : error ? (
          <div className="p-8">
            <div className="text-center mb-4">
              <p className="text-destructive font-semibold mb-2">Error loading files</p>
              <p className="text-sm text-muted-foreground mb-4">{error}</p>
              <div className="text-xs text-muted-foreground bg-muted p-3 rounded mb-4">
                <div>Backup ID: {backupId}</div>
                <div>Device ID: {deviceId}</div>
                <div>Share ID: {shareId}</div>
                <div>Path: {currentPath}</div>
              </div>
            </div>
            <div className="text-center">
              <button
                onClick={() => loadFiles(currentPath)}
                className="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
              >
                Retry
              </button>
            </div>
          </div>
        ) : files.length === 0 ? (
          <div className="p-8 text-center text-muted-foreground">
            This folder is empty
          </div>
        ) : (
          <table className="w-full">
            <thead className="bg-muted border-b border-border">
              <tr>
                <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
                  Name
                </th>
                <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
                  Size
                </th>
                <th className="text-left px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
                  Modified
                </th>
                <th className="text-right px-4 py-3 text-sm font-medium text-muted-foreground uppercase tracking-wide">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody>
              {files.map((file, index) => (
                <tr
                  key={`${file.path}-${index}`}
                  className="border-b border-border last:border-0 hover:bg-muted/50 transition-colors"
                >
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      {file.isDirectory ? (
                        <Folder className="w-4 h-4 text-blue-500" />
                      ) : (
                        <File className="w-4 h-4 text-muted-foreground" />
                      )}
                      {file.isDirectory ? (
                        <button
                          onClick={() => navigateToFolder(file.path)}
                          className="font-medium text-foreground hover:text-primary transition-colors"
                        >
                          {file.name}
                        </button>
                      ) : (
                        <span className="font-medium text-foreground">{file.name}</span>
                      )}
                    </div>
                  </td>
                  
                  <td className="px-4 py-3">
                    <span className="text-sm text-muted-foreground">
                      {file.isDirectory ? '-' : formatFileSize(file.size)}
                    </span>
                  </td>
                  
                  <td className="px-4 py-3">
                    <span className="text-sm text-muted-foreground">{formatDate(file.modifiedAt)}</span>
                  </td>
                  
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-2">
                      {file.isDirectory ? (
                        <button
                          onClick={() => navigateToFolder(file.path)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-foreground bg-card hover:bg-muted border border-border rounded-md transition-colors whitespace-nowrap"
                        >
                          <Folder className="w-3.5 h-3.5" />
                          Browse
                        </button>
                      ) : (
                        <button
                          onClick={() => downloadFile(file)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-primary-foreground bg-primary hover:bg-primary/90 rounded-md transition-colors whitespace-nowrap"
                        >
                          <Download className="w-3.5 h-3.5" />
                          Download
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
