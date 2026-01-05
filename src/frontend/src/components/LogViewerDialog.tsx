import { Download, Copy, ChevronDown, ChevronRight, AlertTriangle, CheckCircle2, XCircle, Clock, FileText, HardDrive, X, TrendingUp } from 'lucide-react';
import { useState } from 'react';

export interface BackupLogData {
  status: 'success' | 'warning' | 'error';
  duration: string;
  filesProcessed: number;
  dataProcessed: string;
  message: string;
  snapshotInfo: {
    snapshotId: string;
    parentSnapshot: string;
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
    dataBlobs: number;
    treeBlobs: number;
    deduplicationRatio: string;
    spaceSaved: string;
  };
  warnings: string[];
  errors: string[];
  progressLog: any[];
}

interface LogViewerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  logData: BackupLogData;
  jobId: string;
}

export function LogViewerDialog({ open, onOpenChange, logData, jobId }: LogViewerDialogProps) {
  const [snapshotExpanded, setSnapshotExpanded] = useState(true);
  const [metricsExpanded, setMetricsExpanded] = useState(true);
  const [warningsExpanded, setWarningsExpanded] = useState(true);
  const [progressLogExpanded, setProgressLogExpanded] = useState(false);

  if (!open) return null;

  const handleExport = () => {
    const dataStr = JSON.stringify(logData, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `backup-log-${jobId}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const handleCopyId = () => {
    navigator.clipboard.writeText(logData.snapshotInfo.snapshotId);
  };

  const statusIcon = {
    success: <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400" />,
    warning: <AlertTriangle className="h-5 w-5 text-yellow-600 dark:text-yellow-400" />,
    error: <XCircle className="h-5 w-5 text-red-600 dark:text-red-400" />,
  }[logData.status];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-5xl bg-white dark:bg-slate-800 rounded-lg shadow-xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="border-b border-gray-200 dark:border-slate-700 px-6 py-4 flex items-center justify-between bg-white dark:bg-slate-800">
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">Backup Summary</h2>
          <div className="flex gap-2 items-center">
            <button
              onClick={handleExport}
              className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-slate-700 border border-gray-300 dark:border-slate-600 rounded-md hover:bg-gray-50 dark:hover:bg-slate-600 transition-colors"
            >
              <Download className="h-4 w-4" />
              Export
            </button>
            <button
              onClick={handleCopyId}
              className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-slate-700 border border-gray-300 dark:border-slate-600 rounded-md hover:bg-gray-50 dark:hover:bg-slate-600 transition-colors"
            >
              <Copy className="h-4 w-4" />
              Copy ID
            </button>
            <button
              onClick={() => onOpenChange(false)}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
        </div>

        {/* Scrollable Content */}
        <div className="overflow-y-auto flex-1 px-6 py-6 bg-gray-50 dark:bg-slate-900">
          <div className="space-y-6">
          {/* Summary Cards */}
          <div className="grid grid-cols-4 gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                {statusIcon}
                <span>Status</span>
              </div>
              <div className="text-lg font-semibold text-gray-900 dark:text-white capitalize">{logData.status}</div>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                <Clock className="h-5 w-5" />
                <span>Duration</span>
              </div>
              <div className="text-lg font-semibold text-gray-900 dark:text-white">{logData.duration}</div>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                <FileText className="h-5 w-5" />
                <span>Files Processed</span>
              </div>
              <div className="text-lg font-semibold text-gray-900 dark:text-white">{logData.filesProcessed.toLocaleString()}</div>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                <HardDrive className="h-5 w-5" />
                <span>Data Processed</span>
              </div>
              <div className="text-lg font-semibold text-gray-900 dark:text-white">{logData.dataProcessed}</div>
            </div>
          </div>

          {/* Status Message Banner */}
          <div className="p-4 rounded-lg bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-700 text-green-800 dark:text-green-300">
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
                      onClick={handleCopyId}
                      className="px-2 py-1 text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-slate-700 rounded transition-colors"
                    >
                      <Copy className="h-4 w-4" />
                    </button>
                  </div>
                </div>
                <div className="space-y-2">
                  <div className="text-sm text-gray-500 dark:text-gray-400">Parent Snapshot</div>
                  <code className="text-sm text-gray-900 dark:text-gray-200 bg-gray-100 dark:bg-slate-700 px-3 py-1 rounded block">{logData.snapshotInfo.parentSnapshot}</code>
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
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.dataStats.dataBlobs.toLocaleString()}</div>
                    </div>
                    <div className="bg-white dark:bg-slate-700 p-3 rounded-lg shadow-md border border-gray-100 dark:border-slate-600">
                      <div className="text-sm text-gray-500 dark:text-gray-400 mb-1">Tree Blobs</div>
                      <div className="text-xl font-semibold text-gray-900 dark:text-white">{logData.dataStats.treeBlobs.toLocaleString()}</div>
                    </div>
                  </div>
                  <div className="mt-4 bg-white dark:bg-slate-700/50 p-4 rounded-lg border border-teal-200 dark:border-teal-600 shadow-md">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <TrendingUp className="h-8 w-8 text-teal-600 dark:text-teal-400" />
                        <div>
                          <div className="text-sm text-gray-600 dark:text-gray-400">Deduplication Ratio</div>
                          <div className="text-2xl font-bold text-gray-900 dark:text-white">{logData.dataStats.deduplicationRatio}</div>
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-sm text-gray-600 dark:text-gray-400">Space Saved</div>
                        <div className="text-2xl font-bold text-teal-700 dark:text-teal-300">{logData.dataStats.spaceSaved}</div>
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
    </div>
  );
}
