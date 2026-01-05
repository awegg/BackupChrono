import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BackupJob, BackupJobStatus as JobStatus } from '../types';
import { backupService } from '../services/deviceService';
import { RefreshCw, CheckCircle, XCircle, Clock, Loader, ChevronDown, ChevronRight, Trash2, StopCircle, FolderOpen } from 'lucide-react';
import BackupProgressBar from './BackupProgressBar';

export default function BackupJobStatusList() {
  const navigate = useNavigate();
  const [jobs, setJobs] = useState<BackupJob[]>([]);
  const [loading, setLoading] = useState(true);
  const [expandedErrors, setExpandedErrors] = useState<Set<string>>(new Set());
  const [expandedCommands, setExpandedCommands] = useState<Set<string>>(new Set());

  const toggleErrorExpansion = (jobId: string) => {
    setExpandedErrors(prev => {
      const next = new Set(prev);
      if (next.has(jobId)) {
        next.delete(jobId);
      } else {
        next.add(jobId);
      }
      return next;
    });
  };

  const toggleCommandExpansion = (jobId: string) => {
    setExpandedCommands(prev => {
      const next = new Set(prev);
      if (next.has(jobId)) {
        next.delete(jobId);
      } else {
        next.add(jobId);
      }
      return next;
    });
  };

  const loadJobs = async () => {
    try {
      const data = await backupService.listJobs();
      setJobs(data);
    } catch (error) {
      console.error('Failed to load backup jobs:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteJob = async (jobId: string) => {
    if (!confirm('Are you sure you want to delete this backup job?')) {
      return;
    }

    try {
      await backupService.deleteJob(jobId);
      await loadJobs();
    } catch (error) {
      console.error('Failed to delete backup job:', error);
      alert('Failed to delete backup job');
    }
  };

  const handleCancelJob = async (jobId: string) => {
    if (!confirm('Are you sure you want to cancel this running backup?')) {
      return;
    }

    try {
      await backupService.cancelJob(jobId);
      await loadJobs();
    } catch (error) {
      console.error('Failed to cancel backup job:', error);
      alert('Failed to cancel backup job');
    }
  };

  const handleBrowseBackup = (job: BackupJob) => {
    // Navigate directly to the file browser for this specific backup
    navigate(`/devices/${job.deviceId}/backups/${job.backupId}/browse`);
  };

  useEffect(() => {
    loadJobs();
    const interval = setInterval(loadJobs, 5000);
    return () => clearInterval(interval);
  }, []);

  const getStatusIcon = (status: JobStatus) => {
    switch (status) {
      case JobStatus.Running:
        return <Loader className="h-5 w-5 animate-spin text-blue-500" />;
      case JobStatus.Completed:
        return <CheckCircle className="h-5 w-5 text-green-500" />;
      case JobStatus.Failed:
        return <XCircle className="h-5 w-5 text-red-500" />;
      case JobStatus.Pending:
        return <Clock className="h-5 w-5 text-yellow-500" />;
      default:
        return <Clock className="h-5 w-5 text-gray-500" />;
    }
  };

  const getStatusColor = (status: JobStatus) => {
    switch (status) {
      case JobStatus.Running:
        return 'text-blue-600 bg-blue-50';
      case JobStatus.Completed:
        return 'text-green-600 bg-green-50';
      case JobStatus.Failed:
        return 'text-red-600 bg-red-50';
      case JobStatus.Pending:
        return 'text-yellow-600 bg-yellow-50';
      default:
        return 'text-gray-600 bg-gray-50';
    }
  };

  const formatTimestamp = (dateString: string | undefined) => {
    if (!dateString) return '-';
    const date = new Date(dateString);
    const day = date.getDate();
    const month = date.getMonth() + 1;
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    return `${day}.${month}., ${hours}:${minutes}`;
  };

  const getFullTimestamp = (dateString: string | undefined) => {
    if (!dateString) return '';
    return new Date(dateString).toLocaleString();
  };

  if (loading) {
    return (
      <div className="flex justify-center items-center p-8">
        <Loader className="h-8 w-8 animate-spin text-blue-500" />
      </div>
    );
  }

  return (
    <div className="bg-white shadow rounded-lg overflow-hidden">
      <div className="px-4 py-5 sm:px-6 flex justify-between items-center">
        <h3 className="text-lg font-medium leading-6 text-gray-900">Backup Jobs</h3>
        <button
          onClick={loadJobs}
          className="inline-flex items-center px-3 py-2 border border-gray-300 shadow-sm text-sm leading-4 font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
        >
          <RefreshCw className="h-4 w-4 mr-2" />
          Refresh
        </button>
      </div>
      
      {jobs.length === 0 ? (
        <div className="px-4 py-5 sm:p-6 text-center text-gray-500">
          No backup jobs found
        </div>
      ) : (
        <div className="overflow-x-auto scrollbar-visible relative">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Device</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Share</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Type</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Started</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Completed</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider w-80">Progress</th>
                <th className="px-3 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {jobs.map((job) => (
                <React.Fragment key={job.id}>
                  <tr>
                    <td className="px-3 py-3 whitespace-nowrap">
                      <div className="flex items-center">
                        {getStatusIcon(job.status)}
                        <span className={`ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${getStatusColor(job.status)}`}>
                          {job.status}
                        </span>
                      </div>
                    </td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm text-gray-900">{job.deviceName || job.deviceId}</td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm text-gray-500">{job.shareName || job.shareId || '-'}</td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm text-gray-500">{job.type}</td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm text-gray-500" title={getFullTimestamp(job.startedAt)}>
                      {formatTimestamp(job.startedAt)}
                    </td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm text-gray-500" title={getFullTimestamp(job.completedAt)}>
                      {formatTimestamp(job.completedAt)}
                    </td>
                    <td className="px-3 py-3 w-80">
                      {job.status === JobStatus.Running ? (
                        <BackupProgressBar jobId={job.id} initialStatus={job.status} job={job} />
                      ) : job.status === JobStatus.Completed ? (
                        <BackupProgressBar jobId={job.id} initialStatus={job.status} job={job} />
                      ) : job.errorMessage ? (
                        <div className="text-xs text-red-600">
                          {job.errorMessage.length > 100 && !expandedErrors.has(job.id) ? (
                            <div>
                              <div className="line-clamp-2">{job.errorMessage}</div>
                              <button
                                onClick={() => toggleErrorExpansion(job.id)}
                                className="mt-1 flex items-center text-red-700 hover:text-red-800 font-medium"
                              >
                                <ChevronDown className="h-3 w-3 mr-1" />
                                Show more
                              </button>
                            </div>
                          ) : (
                            <div>
                              <div className="whitespace-pre-wrap break-words">{job.errorMessage}</div>
                              {job.errorMessage.length > 100 && (
                                <button
                                  onClick={() => toggleErrorExpansion(job.id)}
                                  className="mt-1 flex items-center text-red-700 hover:text-red-800 font-medium"
                                >
                                  <ChevronRight className="h-3 w-3 mr-1" />
                                  Show less
                                </button>
                              )}
                            </div>
                          )}
                        </div>
                      ) : (
                        <span className="text-xs text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-3 py-3 whitespace-nowrap text-sm">
                      <div className="flex items-center gap-2">
                        {job.status === JobStatus.Completed && job.backupId && (
                          <button
                            onClick={() => handleBrowseBackup(job)}
                            className="text-blue-600 hover:text-blue-900"
                            title="Browse backup files"
                          >
                            <FolderOpen className="h-4 w-4" />
                          </button>
                        )}
                        {job.status === JobStatus.Running ? (
                          <button
                            onClick={() => handleCancelJob(job.id)}
                            className="text-orange-600 hover:text-orange-900"
                            title="Stop backup"
                          >
                            <StopCircle className="h-4 w-4" />
                          </button>
                        ) : (
                          <button
                            onClick={() => handleDeleteJob(job.id)}
                            className="text-red-600 hover:text-red-900"
                            title="Delete job"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                  {job.commandLine && (
                    <tr className="bg-gray-50">
                      <td colSpan={8} className="px-3 py-2">
                        <div className="text-xs">
                          <button
                            onClick={() => toggleCommandExpansion(job.id)}
                            className="flex items-center text-gray-700 hover:text-gray-900 font-medium"
                          >
                            {expandedCommands.has(job.id) ? (
                              <ChevronDown className="h-3 w-3 mr-1" />
                            ) : (
                              <ChevronRight className="h-3 w-3 mr-1" />
                            )}
                            Command Line
                          </button>
                          {expandedCommands.has(job.id) && (
                            <div className="mt-2 p-2 bg-gray-800 text-gray-100 rounded font-mono text-xs overflow-x-auto">
                              {job.commandLine}
                            </div>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
