/**
 * Shared types for backup log data used across LogViewerDialog and BackupLogViewerPage
 */

export interface BackupLogData {
  // Computed/formatted display fields
  status: 'success' | 'warning' | 'error';
  duration: string;
  filesProcessed: number;
  dataProcessed: string;
  message: string;
  
  // Snapshot information
  snapshotInfo: {
    snapshotId: string;
    parentSnapshot: string | null;
    timestamp: string;
    exitCode: number;
  };
  
  // File statistics
  fileStats: {
    total: number;
    new: number;
    changed: number;
    unmodified: number;
  };
  
  // Directory statistics
  directoryStats: {
    total: number;
    new: number;
    changed: number;
    unmodified: number;
  };
  
  // Data statistics
  dataStats: {
    totalProcessed: string;
    dataAdded: string;
  };
  
  // Deduplication information
  deduplicationInfo: {
    dataBlobs: number;
    treeBlobs: number;
    ratio: string;
    spaceSaved: string;
    contentDedup: string;
    uniqueStorage: string;
  };
  
  // Logs
  warnings: string[];
  errors: string[];
  progressLog: any[];
}
