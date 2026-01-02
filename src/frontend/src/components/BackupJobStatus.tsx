import { useState, useEffect } from 'react';
import { BackupJob, BackupJobStatus as JobStatus } from '../types';
import { backupService } from '../services/deviceService';
import { RefreshCw, CheckCircle, XCircle, Clock, Loader } from 'lucide-react';
import BackupProgressBar from './BackupProgressBar';

export default function BackupJobStatusList() {
  const [jobs, setJobs] = useState<BackupJob[]>([]);
  const [loading, setLoading] = useState(true);

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
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {jobs.map((job) => (
                <tr key={job.id}>
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
                      <BackupProgressBar jobId={job.id} initialStatus={job.status} />
                    ) : job.errorMessage ? (
                      <span className="text-xs text-red-600">{job.errorMessage}</span>
                    ) : (
                      <span className="text-xs text-gray-400">-</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
