import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Device } from '../types';
import { deviceService } from '../services/deviceService';
import { healthService } from '../services/healthService';
import { RefreshCw, AlertTriangle } from 'lucide-react';
import { DeviceList } from '../components/DeviceList';
import BackupJobStatusList from '../components/BackupJobStatus';
import { HealthStatusPanel } from '../components/HealthStatusPanel';

export default function Dashboard() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [backendOffline, setBackendOffline] = useState(false);
  const navigate = useNavigate();

  const checkBackendHealth = async () => {
    const isHealthy = await healthService.checkAvailability();
    setBackendOffline(!isHealthy);
    return isHealthy;
  };

  const loadDevices = async () => {
    try {
      setLoading(true);
      setError(null);
      
      // Check backend health first
      const isBackendHealthy = await checkBackendHealth();
      if (!isBackendHealthy) {
        setError('Backend server is not running. Please start the API server.');
        return;
      }

      const data = await deviceService.listDevices();
      setDevices(data);
    } catch (err) {
      setError('Failed to load devices');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDevices();
  }, []);

  if (loading) {
    return <div className="flex items-center justify-center h-screen">
      <RefreshCw className="w-8 h-8 animate-spin text-blue-500" />
    </div>;
  }

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <h1 className="text-3xl font-bold mb-6">BackupChrono Dashboard</h1>

      {error && (
        <div className={`border px-4 py-3 rounded mb-4 ${
          backendOffline 
            ? 'bg-yellow-100 border-yellow-400 text-yellow-800' 
            : 'bg-red-100 border-red-400 text-red-700'
        }`}>
          <div className="flex items-center">
            <AlertTriangle className="w-5 h-5 mr-2" />
            <div>
              <div className="font-semibold">{backendOffline ? 'Backend Server Offline' : 'Error'}</div>
              <div className="text-sm">{error}</div>
              {backendOffline && (
                <div className="text-sm mt-2">
                  Expected backend URL: <code className="bg-yellow-200 px-1 rounded">{import.meta.env.VITE_API_URL || 'http://localhost:5000'}</code>
                  <br />
                  <button 
                    onClick={loadDevices}
                    className="mt-2 text-yellow-900 underline hover:no-underline"
                  >
                    Retry connection
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6">
        {/* Health Status Panel */}
        <HealthStatusPanel />
        
        {/* Devices List */}
        <DeviceList devices={devices} onDeviceUpdated={loadDevices} />
        
        {/* Backup Jobs Status */}
        <div className="mt-6">
          <BackupJobStatusList />
        </div>
      </div>
    </div>
  );
}

