import { useState, useEffect } from 'react';
import { BackupProgress, signalRService } from '../services/signalr';
import { BackupJob } from '../types';
import { Loader, CheckCircle, XCircle, FileText } from 'lucide-react';

interface BackupProgressBarProps {
  jobId: string;
  initialStatus?: string;
  job?: BackupJob;
}

export default function BackupProgressBar({ jobId, initialStatus, job }: BackupProgressBarProps) {
  const [progress, setProgress] = useState<BackupProgress | null>(null);
  const [lastFilename, setLastFilename] = useState<string>('');
  const [transferRate, setTransferRate] = useState<number>(0);
  const [lastBytes, setLastBytes] = useState<{ bytes: number; timestamp: number } | null>(null);

  useEffect(() => {
    const handleProgress = (p: BackupProgress) => {
      if (p.jobId === jobId) {
        setProgress(p);
        // Remember the last filename if one is provided
        if (p.currentFile) {
          setLastFilename(p.currentFile);
        }
        
        // Calculate transfer rate
        if (p.bytesProcessed !== undefined) {
          const now = Date.now();
          if (lastBytes) {
            const bytesDiff = p.bytesProcessed - lastBytes.bytes;
            const timeDiff = (now - lastBytes.timestamp) / 1000; // Convert to seconds
            if (timeDiff > 0 && bytesDiff > 0) {
              const rate = bytesDiff / timeDiff;
              setTransferRate(rate);
            }
          }
          setLastBytes({ bytes: p.bytesProcessed, timestamp: now });
        }
      }
    };

    signalRService.subscribe(jobId, handleProgress);

    return () => {
      signalRService.unsubscribe(jobId);
    };
  }, [jobId, lastBytes]);

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
      {(isRunning || isCompleted || isFailed) && (
        <div className="text-xs text-gray-600 space-y-0.5 ml-6">
          {(currentProgress.filesProcessed !== undefined || currentProgress.totalFiles !== undefined || (job && job.filesProcessed)) && (
            <div className="flex items-center gap-2">
              <FileText className="h-3 w-3" />
              <span>
                Files: {((isRunning ? currentProgress.filesProcessed : job?.filesProcessed) || 0).toLocaleString()}{currentProgress.totalFiles ? ` / ${currentProgress.totalFiles.toLocaleString()}` : ''}
              </span>
            </div>
          )}
          
          {((currentProgress.bytesProcessed !== undefined && currentProgress.totalBytes !== undefined) || (job && job.bytesTransferred)) && (
            <div className="flex items-center gap-2">
              <span>
                Data: {formatBytes((isRunning ? currentProgress.bytesProcessed : job?.bytesTransferred) || 0)}{isRunning && currentProgress.totalBytes ? ` / ${formatBytes(currentProgress.totalBytes)}` : ''}
                {isRunning && transferRate > 0 && (
                  <span className="ml-2 text-blue-600">
                    ({formatBytes(transferRate)}/s)
                  </span>
                )}
                {!isRunning && job && job.bytesTransferred && job.startedAt && job.completedAt && (() => {
                  const duration = (new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()) / 1000;
                  const avgRate = duration > 0 ? job.bytesTransferred / duration : 0;
                  return avgRate > 0 ? (
                    <span className="ml-2 text-green-600">
                      (avg: {formatBytes(avgRate)}/s)
                    </span>
                  ) : null;
                })()}
              </span>
            </div>
          )}
          
          <div className="flex items-center gap-2 text-gray-500 min-w-0 min-h-[1.25rem]">
            {lastFilename && (
              <span className="truncate max-w-xs" title={lastFilename} dir="rtl">
                {lastFilename}
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
