import { Download, Copy, ChevronDown, ChevronRight, AlertTriangle, CheckCircle2, XCircle, Clock, FileText, HardDrive, ArrowLeft, TrendingUp, FolderOpen } from 'lucide-react';
import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams, useParams } from 'react-router-dom';
import { AxiosError } from 'axios';
import { getBackupDetail, getBackupLogs } from '../services/backupsApi';

// OpenAPI: BackupDetail schema
export interface BackupDetail {
  // From Backup
  id: string;
  deviceId: string;
  shareId?: string | null;
  deviceName: string;
  shareName?: string | null;
  timestamp: string;
  status: 'Success' | 'Partial' | 'Failed';
  sharesPaths: Record<string, string>;
  fileStats: {
    new: number;
    changed: number;
    unmodified: number;
  };
  dataStats: {
    added: number; // bytes
    processed: number; // bytes
  };
  duration: string;
  errorMessage?: string | null;
  createdByJobId?: string;
  // BackupDetail specific
  directoryStats: {
    new: number;
    changed: number;
    unmodified: number;
  };
  snapshotInfo: {
    snapshotId: string;
    parentSnapshot: string | null;
    exitCode: number;
  };
  deduplicationInfo: {
    dataBlobs: number;
    treeBlobs: number;
    ratio: string;
    spaceSaved: string;
  };
  shares: Array<{
    name: string;
    path: string;
    fileCount: number;
    size: number;
  }>;
}

// OpenAPI: BackupLogs schema
export interface BackupLogs {
  warnings: string[];
  errors: string[];
  progressLog: Array<{
    timestamp: string;
    message: string;
    percentDone?: number;
    currentFiles?: string[];
    filesDone?: number;
    bytesDone?: number;
  }>;
}

// Combined data for UI display
export interface BackupLogData {
  // Computed/formatted display fields
  status: 'success' | 'warning' | 'error';
  duration: string;
  filesProcessed: number;
  dataProcessed: string;
  message: string;
  // From BackupDetail
  snapshotInfo: {
    snapshotId: string;
    parentSnapshot: string | null;
    timestamp: string;
    exitCode: number;
  };
  fileStats: {
    total: number;
    new: number;
    changed: number;
    unmodified: number;
  };
  directoryStats: {
    total: number;
    new: number;
    changed: number;
    unmodified: number;
  };
  dataStats: {
    totalProcessed: string;
    dataAdded: string;
  };
  deduplicationInfo: {
    dataBlobs: number;
    treeBlobs: number;
    ratio: string;
    spaceSaved: string;
  };
  // From BackupLogs
  warnings: string[];
  errors: string[];
  progressLog: any[];
}

// Helper function to format bytes to human-readable string
function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

// Helper function to merge BackupDetail and BackupLogs into display format
function mergeBackupData(detail: BackupDetail, logs: BackupLogs): BackupLogData {
  const totalFiles = detail.fileStats.new + detail.fileStats.changed + detail.fileStats.unmodified;
  const totalDirs = detail.directoryStats.new + detail.directoryStats.changed + detail.directoryStats.unmodified;
  
  // Determine status based on errors/warnings
  let status: 'success' | 'warning' | 'error' = 'success';
  if (logs.errors.length > 0) {
    status = 'error';
  } else if (logs.warnings.length > 0 || detail.status === 'Partial') {
    status = 'warning';
  } else if (detail.status === 'Failed') {
    status = 'error';
  }
  
  // Create message
  let message = 'Backup completed successfully. All files processed without errors.';
  if (status === 'error') {
    message = `Backup failed with ${logs.errors.length} error(s). Please review the error log below.`;
  } else if (status === 'warning') {
    message = `Backup completed with ${logs.warnings.length} warning(s). Some files may have been skipped.`;
  }
  
  return {
    status,
    duration: detail.duration,
    filesProcessed: totalFiles,
    dataProcessed: formatBytes(detail.dataStats.processed),
    message,
    snapshotInfo: {
      ...detail.snapshotInfo,
      timestamp: detail.timestamp, // Add timestamp from root level
    },
    fileStats: {
      total: totalFiles,
      ...detail.fileStats,
    },
    directoryStats: {
      total: totalDirs,
      ...detail.directoryStats,
    },
    dataStats: {
      totalProcessed: formatBytes(detail.dataStats.processed),
      dataAdded: formatBytes(detail.dataStats.added),
    },
    deduplicationInfo: detail.deduplicationInfo,
    warnings: logs.warnings,
    errors: logs.errors,
    progressLog: logs.progressLog,
  };
}

export function BackupLogViewerPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { backupId } = useParams<{ backupId: string }>();

  const deviceId = searchParams.get('deviceId') ?? undefined;
  const shareId = searchParams.get('shareId') ?? undefined;

  const [logData, setLogData] = useState<BackupLogData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [snapshotExpanded, setSnapshotExpanded] = useState(true);
  const [metricsExpanded, setMetricsExpanded] = useState(true);
  const [warningsExpanded, setWarningsExpanded] = useState(true);
  const [progressLogExpanded, setProgressLogExpanded] = useState(false);

  useEffect(() => {
    if (!backupId) return;

    let isCancelled = false;
    setLoading(true);
    setError(null);

    (async () => {
      try {
        const [detail, logs] = await Promise.all([
          getBackupDetail(backupId, deviceId, shareId),
          getBackupLogs(backupId, deviceId, shareId)
        ]);

        if (!isCancelled) {
          setLogData(mergeBackupData(detail as BackupDetail, logs as BackupLogs));
        }
      } catch (err: any) {
        if (!isCancelled) {
          const axiosErr = err as AxiosError<{ error?: string; detail?: string }>;
          const detail = axiosErr.response?.data?.detail;
          const friendly = detail || axiosErr.response?.data?.error || axiosErr.message;
          setError(friendly ?? 'Failed to load backup logs');
        }
      } finally {
        if (!isCancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      isCancelled = true;
    };
  }, [backupId, deviceId, shareId]);

  if (!backupId) {
    return (
      <div className="p-6 text-red-600 dark:text-red-400">Backup ID is missing from the URL.</div>
    );
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-50 dark:bg-slate-900 flex items-center justify-center">
        <div className="text-gray-600 dark:text-gray-300">Loading backup logs...</div>
      </div>
    );
  }

  if (error || !logData) {
    return (
      <div className="min-h-screen bg-gray-50 dark:bg-slate-900 flex items-center justify-center">
        <div className="space-y-3">
          <div className="bg-red-100 dark:bg-red-900/40 border border-red-300 dark:border-red-700 text-red-800 dark:text-red-200 px-4 py-3 rounded">
            {error ?? 'Unable to load backup log data.'}
          </div>
          <div className="flex justify-center gap-3">
            <button
              onClick={() => navigate(-1)}
              className="px-4 py-2 rounded border border-gray-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-slate-700"
            >
              Go Back
            </button>
            <button
              onClick={() => navigate('/backups')}
              className="px-4 py-2 rounded bg-blue-600 text-white hover:bg-blue-700"
            >
              View Backups
            </button>
          </div>
        </div>
      </div>
    );
  }

  const handleExport = () => {
    const dataStr = JSON.stringify(logData, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `backup-log-${backupId}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const handleBrowseBackups = () => {
    navigate(`/backups/${backupId}/browse`);
  };

  const statusIcon = {
    success: <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400" />,
    warning: <AlertTriangle className="h-5 w-5 text-yellow-600 dark:text-yellow-400" />,
    error: <XCircle className="h-5 w-5 text-red-600 dark:text-red-400" />,
  }[logData.status];

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-slate-900">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="mb-6">
          <button
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200 mb-4"
          >
            <ArrowLeft className="h-4 w-4" />
            Back
          </button>
          
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900 dark:text-white">Backup Summary</h1>
              <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">Backup ID: {backupId}</p>
            </div>
            <div className="flex gap-2">
              <button
                onClick={handleExport}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-slate-700 border border-gray-300 dark:border-slate-600 rounded-md hover:bg-gray-50 dark:hover:bg-slate-600 transition-colors"
              >
                <Download className="h-4 w-4" />
                Export
              </button>
              <button
                onClick={handleBrowseBackups}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-slate-700 border border-gray-300 dark:border-slate-600 rounded-md hover:bg-gray-50 dark:hover:bg-slate-600 transition-colors"
              >
                <FolderOpen className="h-4 w-4" />
                Browse Backups
              </button>
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="space-y-6">
          {/* Summary Cards */}
          <div className="grid grid-cols-4 gap-4">
            <div className="bg-white dark:bg-slate-800 p-6 rounded-lg shadow-md border border-gray-200 dark:border-slate-700">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-2">
                {statusIcon}
                <span>Status</span>
              </div>
              <div className="text-xl font-semibold text-gray-900 dark:text-white capitalize">{logData.status}</div>
            </div>
            <div className="bg-white dark:bg-slate-800 p-6 rounded-lg shadow-md border border-gray-200 dark:border-slate-700">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-2">
                <Clock className="h-5 w-5" />
                <span>Duration</span>
              </div>
              <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.duration}</div>
            </div>
            <div className="bg-white dark:bg-slate-800 p-6 rounded-lg shadow-md border border-gray-200 dark:border-slate-700">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-2">
                <FileText className="h-5 w-5" />
                <span>Files Processed</span>
              </div>
              <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.filesProcessed.toLocaleString()}</div>
            </div>
            <div className="bg-white dark:bg-slate-800 p-6 rounded-lg shadow-md border border-gray-200 dark:border-slate-700">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-2">
                <HardDrive className="h-5 w-5" />
                <span>Data Processed</span>
              </div>
              <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.dataProcessed}</div>
            </div>
          </div>

          {/* Status Message Banner */}
          <div className={`p-4 rounded-lg ${
            logData.status === 'error' 
              ? 'bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 text-red-800 dark:text-red-300'
              : logData.status === 'warning'
                ? 'bg-yellow-50 dark:bg-yellow-900/30 border border-yellow-200 dark:border-yellow-700 text-yellow-800 dark:text-yellow-300'
                : 'bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-700 text-green-800 dark:text-green-300'
          }`}>
            {logData.message}
          </div>
          {/* Snapshot Information */}
          <div className="bg-white dark:bg-slate-800 border border-gray-200 dark:border-slate-700 rounded-lg shadow-md">
            <button
              onClick={() => setSnapshotExpanded(!snapshotExpanded)}
              className="w-full flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
            >
              <div className="flex items-center gap-2 font-semibold text-gray-900 dark:text-white">
                {snapshotExpanded ? <ChevronDown className="h-5 w-5" /> : <ChevronRight className="h-5 w-5" />}
                Snapshot Information
              </div>
            </button>
            {snapshotExpanded && (
              <div className="p-4 pt-0 grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <div className="text-sm text-gray-500 dark:text-gray-400">Snapshot ID</div>
                  <div className="flex items-center gap-2">
                    <code className="text-sm text-gray-900 dark:text-gray-200 bg-gray-100 dark:bg-slate-700 px-3 py-1 rounded flex-1">{logData.snapshotInfo.snapshotId}</code>
                    <button 
                      onClick={() => navigator.clipboard.writeText(logData.snapshotInfo.snapshotId)}
                      className="px-2 py-1 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-slate-700 rounded transition-colors"
                    >
                      <Copy className="h-4 w-4" />
                    </button>
                  </div>
                </div>
                <div className="space-y-2">
                  <div className="text-sm text-gray-500 dark:text-gray-400">Parent Snapshot</div>
                  <code className="text-sm text-gray-900 dark:text-gray-200 bg-gray-100 dark:bg-slate-700 px-3 py-1 rounded block">
                    {logData.snapshotInfo.parentSnapshot || 'None (Initial Backup)'}
                  </code>
                </div>
                <div className="space-y-2">
                  <div className="text-sm text-gray-500 dark:text-gray-400">Timestamp</div>
                  <code className="text-sm text-gray-900 dark:text-gray-200 bg-gray-100 dark:bg-slate-700 px-3 py-1 rounded block">{logData.snapshotInfo.timestamp}</code>
                </div>
                <div className="space-y-2">
                  <div className="text-sm text-gray-500 dark:text-gray-400">Exit Code</div>
                  <span className={`inline-flex items-center px-2.5 py-1 rounded text-sm ${logData.snapshotInfo.exitCode === 0 ? 'bg-green-100 dark:bg-green-900/40 text-green-800 dark:text-green-300 border border-green-200 dark:border-green-700' : 'bg-red-100 dark:bg-red-900/40 text-red-800 dark:text-red-300 border border-red-200 dark:border-red-700'}`}>
                    {logData.snapshotInfo.exitCode} ({logData.snapshotInfo.exitCode === 0 ? 'Success' : 'Failed'})
                  </span>                
                  </div>
              </div>
            )}
          </div>

          {/* Detailed Metrics */}
          <div className="bg-white dark:bg-slate-800 border border-gray-200 dark:border-slate-700 rounded-lg shadow-md">
            <button
              onClick={() => setMetricsExpanded(!metricsExpanded)}
              className="w-full flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
            >
              <div className="flex items-center gap-2 font-semibold text-gray-900 dark:text-white">
                {metricsExpanded ? <ChevronDown className="h-5 w-5" /> : <ChevronRight className="h-5 w-5" />}
                Detailed Metrics
              </div>
            </button>
            {metricsExpanded && (
              <div className="p-4 pt-0 space-y-6">
                {/* File Statistics */}
                <div>
                  <div className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                    <FileText className="h-4 w-4" />
                    File Statistics
                  </div>
                  <div className="grid grid-cols-4 gap-4">
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Total Files</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.fileStats.total.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-teal-200 dark:border-teal-600">
                      <div className="text-sm text-teal-700 dark:text-teal-400 mb-1">New Files</div>
                      <div className="text-xl font-semibold text-teal-700 dark:text-teal-300">{logData.fileStats.new.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-orange-200 dark:border-orange-600">
                      <div className="text-sm text-orange-700 dark:text-orange-400 mb-1">Changed Files</div>
                      <div className="text-xl font-semibold text-orange-700 dark:text-orange-300">{logData.fileStats.changed.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Unmodified Files</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.fileStats.unmodified.toLocaleString()}</div>
                    </div>
                  </div>
                </div>

                {/* Directory Statistics */}
                <div>
                  <div className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                    <HardDrive className="h-4 w-4" />
                    Directory Statistics
                  </div>
                  <div className="grid grid-cols-4 gap-4">
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Total Directories</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.directoryStats.total.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-teal-200 dark:border-teal-600">
                      <div className="text-sm text-teal-700 dark:text-teal-400 mb-1">New Directories</div>
                      <div className="text-xl font-semibold text-teal-700 dark:text-teal-300">{logData.directoryStats.new}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-orange-200 dark:border-orange-600">
                      <div className="text-sm text-orange-700 dark:text-orange-400 mb-1">Changed Directories</div>
                      <div className="text-xl font-semibold text-orange-700 dark:text-orange-300">{logData.directoryStats.changed}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Unmodified Directories</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.directoryStats.unmodified.toLocaleString()}</div>
                    </div>
                  </div>
                </div>

                {/* Data & Deduplication */}
                <div>
                  <div className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">
                    <HardDrive className="h-4 w-4" />
                    Data & Deduplication
                  </div>
                  <div className="grid grid-cols-4 gap-4">
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Total Processed</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.dataStats.totalProcessed}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-blue-200 dark:border-blue-600">
                      <div className="text-sm text-blue-700 dark:text-blue-400 mb-1">Data Added</div>
                      <div className="text-xl font-semibold text-blue-700 dark:text-blue-300">{logData.dataStats.dataAdded}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Data Blobs</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.deduplicationInfo.dataBlobs.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Tree Blobs</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.deduplicationInfo.treeBlobs.toLocaleString()}</div>
                    </div>
                  </div>
                  <div className="mt-4 bg-white dark:bg-slate-700/50 p-4 rounded-lg border border-teal-200 dark:border-teal-600 shadow-md">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <TrendingUp className="h-8 w-8 text-teal-600 dark:text-teal-400" />
                        <div>
                          <div className="text-sm text-gray-600 dark:text-gray-400">Deduplication Ratio</div>
                          <div className="text-2xl font-bold text-gray-900 dark:text-white">{logData.deduplicationInfo.ratio}</div>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-sm text-gray-600 dark:text-gray-400">Space Saved</div>
                        <div className="text-2xl font-bold text-teal-700 dark:text-teal-300">{logData.deduplicationInfo.spaceSaved}</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Warnings */}
          {logData.warnings.length > 0 && (
            <div className="bg-white dark:bg-slate-800 border rounded-lg border-yellow-200 dark:border-orange-700 shadow-md">
              <button
                onClick={() => setWarningsExpanded(!warningsExpanded)}
                className="w-full flex items-center justify-between p-4 hover:bg-yellow-50 dark:hover:bg-slate-700 transition-colors"
              >
                <div className="flex items-center gap-2 font-semibold text-yellow-800 dark:text-orange-400">
                  {warningsExpanded ? <ChevronDown className="h-5 w-5" /> : <ChevronRight className="h-5 w-5" />}
                  Warnings ({logData.warnings.length})
                </div>
              </button>
              {warningsExpanded && (
                <div className="p-4 pt-0 space-y-3">
                  {logData.warnings.map((warning, index) => (
                    <div key={index} className="bg-yellow-50 dark:bg-orange-900/20 border border-yellow-300 dark:border-orange-700 rounded-lg p-3 flex items-start gap-3 shadow-md">
                      <AlertTriangle className="h-5 w-5 text-yellow-600 dark:text-orange-400 flex-shrink-0 mt-0.5" />
                      <code className="text-sm text-yellow-900 dark:text-orange-200 flex-1">{warning}</code>
                      <button
                        onClick={() => navigator.clipboard.writeText(warning)}
                        className="px-2 py-1 text-yellow-600 dark:text-orange-400 hover:text-yellow-900 dark:hover:text-orange-300 hover:bg-yellow-100 dark:hover:bg-orange-900/30 rounded transition-colors"
                      >
                        <Copy className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Errors */}
          {logData.errors.length > 0 && (
            <div className="bg-white dark:bg-slate-800 border rounded-lg border-red-200 dark:border-red-700 shadow-md">
              <button
                onClick={() => setWarningsExpanded(!warningsExpanded)}
                className="w-full flex items-center justify-between p-4 hover:bg-red-50 dark:hover:bg-slate-700 transition-colors"
              >
                <div className="flex items-center gap-2 font-semibold text-red-800 dark:text-red-400">
                  {warningsExpanded ? <ChevronDown className="h-5 w-5" /> : <ChevronRight className="h-5 w-5" />}
                  Errors ({logData.errors.length})
                </div>
              </button>
              {warningsExpanded && (
                <div className="p-4 pt-0 space-y-3">
                  {logData.errors.map((error, index) => (
                    <div key={index} className="bg-red-50 dark:bg-red-900/20 border border-red-300 dark:border-red-700 rounded-lg p-3 flex items-start gap-3 shadow-md">
                      <XCircle className="h-5 w-5 text-red-600 dark:text-red-400 flex-shrink-0 mt-0.5" />
                      <code className="text-sm text-red-900 dark:text-red-200 flex-1">{error}</code>
                      <button
                        onClick={() => navigator.clipboard.writeText(error)}
                        className="px-2 py-1 text-red-600 dark:text-red-400 hover:text-red-900 dark:hover:text-red-300 hover:bg-red-100 dark:hover:bg-red-900/30 rounded transition-colors"
                      >
                        <Copy className="h-4 w-4" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Progress Log (JSON) */}
          <div className="bg-white dark:bg-slate-800 border border-gray-200 dark:border-slate-700 rounded-lg shadow-md">
            <button
              onClick={() => setProgressLogExpanded(!progressLogExpanded)}
              className="w-full flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-slate-700 transition-colors"
            >
              <div className="flex items-center gap-2 font-semibold text-gray-900 dark:text-white">
                {progressLogExpanded ? <ChevronDown className="h-5 w-5" /> : <ChevronRight className="h-5 w-5" />}
                Progress Log (JSON)
              </div>
            </button>
            {progressLogExpanded && (
              <div className="p-4 pt-0">
                <div className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">Progress Log (JSON)</div>
                <pre className="bg-gray-100 dark:bg-slate-950 text-gray-900 dark:text-gray-100 p-4 rounded-lg text-xs overflow-x-auto max-h-96 overflow-y-auto text-left border border-gray-200 dark:border-slate-700">
                  {JSON.stringify(logData.progressLog, null, 2)}
                </pre>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
