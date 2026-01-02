import { useState, useEffect } from 'react';
import { BackupProgress, signalRService } from '../services/signalr';
import { Loader, CheckCircle, XCircle, FileText } from 'lucide-react';

interface BackupProgressBarProps {
  jobId: string;
  initialStatus?: string;
}

export default function BackupProgressBar({ jobId, initialStatus }: BackupProgressBarProps) {
  const [progress, setProgress] = useState<BackupProgress | null>(null);

  useEffect(() => {
    const handleProgress = (p: BackupProgress) => {
      if (p.jobId === jobId) {
        setProgress(p);
      }
    };

    signalRService.subscribe(jobId, handleProgress);

    return () => {
      signalRService.unsubscribe(jobId);
    };
  }, [jobId]);

  if (!progress && initialStatus !== 'Running') {
    return null;
  }

  const currentProgress = progress || {
    jobId,
    deviceName: '',
    shareName: '',
    status: initialStatus || 'Running',
    percentComplete: 0,
  };

  const isRunning = currentProgress.status === 'Running';
  const isCompleted = currentProgress.status === 'Completed';
  const isFailed = currentProgress.status === 'Failed';

  const formatBytes = (bytes?: number) => {
    if (!bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  };

  return (
    <div className="space-y-1">
      {/* Progress Bar */}
      <div className="flex items-center gap-2">
        {isRunning && <Loader className="h-4 w-4 animate-spin text-blue-500 flex-shrink-0" />}
        {isCompleted && <CheckCircle className="h-4 w-4 text-green-500 flex-shrink-0" />}
        {isFailed && <XCircle className="h-4 w-4 text-red-500 flex-shrink-0" />}
        
        <div className="flex-1 min-w-0">
          <div className="w-full bg-gray-200 rounded-full h-2">
            <div
              className={`h-2 rounded-full transition-all duration-300 ${
                isCompleted ? 'bg-green-500' :
                isFailed ? 'bg-red-500' :
                'bg-blue-500'
              }`}
              style={{ width: `${currentProgress.percentComplete || 0}%` }}
            />
          </div>
        </div>

        <span className="text-xs font-medium text-gray-700 min-w-[2.5rem] text-right">
          {Math.round(currentProgress.percentComplete || 0)}%
        </span>
      </div>

      {/* Details */}
      {isRunning && (
        <div className="text-xs text-gray-600 space-y-0.5 ml-6">
          {(currentProgress.filesProcessed !== undefined || currentProgress.totalFiles !== undefined) && (
            <div className="flex items-center gap-2">
              <FileText className="h-3 w-3" />
              <span>
                Files: {(currentProgress.filesProcessed || 0).toLocaleString()}{currentProgress.totalFiles ? ` / ${currentProgress.totalFiles.toLocaleString()}` : ''}
              </span>
            </div>
          )}
          
          {currentProgress.bytesProcessed !== undefined && currentProgress.totalBytes !== undefined && (
            <div className="flex items-center gap-2">
              <span>
                Data: {formatBytes(currentProgress.bytesProcessed)} / {formatBytes(currentProgress.totalBytes)}
              </span>
            </div>
          )}
          
          <div className="flex items-center gap-2 text-gray-500 min-w-0 min-h-[1.25rem]">
            {currentProgress.currentFile && (
              <span className="truncate-left max-w-xs" title={currentProgress.currentFile} dir="rtl">
                {currentProgress.currentFile}
              </span>
            )}
          </div>
        </div>
      )}

      {isFailed && currentProgress.errorMessage && (
        <div className="text-xs text-red-600 ml-7">
          Error: {currentProgress.errorMessage}
        </div>
      )}
    </div>
  );
}
