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
    // Navigate to the file browser for this specific backup
    navigate(`/devices/${deviceId}/backups/${backup.id}/browse`);
  };

  const handleBack = () => {
    navigate('/');
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-6">
        <button
          onClick={handleBack}
          className="inline-flex items-center text-sm text-gray-600 hover:text-gray-900 mb-4"
        >
          <ChevronLeft size={16} className="mr-1" />
          Back to Devices
        </button>
        
        <div className="flex items-center space-x-3">
          <HardDrive className="text-gray-400" size={32} />
          <div>
            <h1 className="text-2xl font-bold text-gray-900">
              Device Backups
            </h1>
          </div>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="mb-6 bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
          {error}
        </div>
      )}

      {/* Content */}
      <div>
        <h2 className="text-lg font-semibold text-gray-900 mb-4">
          Available Backups
        </h2>
        <BackupsListComponent
          backups={backups}
          onBackupClick={handleBackupSelect}
          loading={loading}
        />
      </div>
    </div>
  );
};
