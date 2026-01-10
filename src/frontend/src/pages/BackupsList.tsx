import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Backup } from '../types';
import { BackupsList as BackupsListComponent } from '../components/BackupsList';
import { ChevronLeft, HardDrive } from 'lucide-react';
import { apiClient } from '../services/api';

export const BackupsListPage: React.FC = () => {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  
  const [backups, setBackups] = useState<Backup[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!deviceId) return;
    
    const loadBackups = async () => {
      setLoading(true);
      setError(null);
      
      try {
        const response = await apiClient.get(`/api/devices/${deviceId}/backups`);
        setBackups(response.data);
      } catch (err) {
        console.error('Error loading backups:', err);
        setError('Failed to load backups');
      } finally {
        setLoading(false);
      }
    };
    
    loadBackups();
  }, [deviceId]);

  const handleBackupSelect = async (backup: Backup) => {
    // Navigate to backup logs/details page
    const params = new URLSearchParams({
      deviceId: deviceId || '',
      shareId: backup.shareId || ''
    });
    navigate(`/backups/${backup.id}/logs?${params}`);
  };

  const handleBrowseBackup = async (backup: Backup) => {
    // Navigate to the file browser for this specific backup
    const params = new URLSearchParams({
      deviceId: deviceId || '',
      shareId: backup.shareId || ''
    });
    navigate(`/devices/${deviceId}/backups/${backup.id}/browse?${params}`);
  };

  const handleBack = () => {
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-900">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="mb-6">
          <button
            onClick={handleBack}
            className="inline-flex items-center text-sm text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 mb-4 transition-colors"
          >
            <ChevronLeft size={16} className="mr-1" />
            Back to Devices
          </button>
          
          <div className="flex items-center space-x-3">
            <div className="p-3 bg-blue-100 dark:bg-blue-900/30 rounded-lg">
              <HardDrive className="text-blue-600 dark:text-blue-400" size={32} />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
                Device Backups
              </h1>
              <p className="text-sm text-slate-600 dark:text-slate-400 mt-1">
                View and browse backup history
              </p>
            </div>
          </div>
        </div>

        {/* Error Display */}
        {error && (
          <div className="mb-6 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-400 px-4 py-3 rounded-lg">
            {error}
          </div>
        )}

        {/* Content */}
        <div>
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white mb-4">
            Available Backups
          </h2>
          <BackupsListComponent
            backups={backups}
            onBackupClick={handleBackupSelect}
            onBrowseClick={handleBrowseBackup}
            loading={loading}
          />
        </div>
      </div>
    </div>
  );
};
