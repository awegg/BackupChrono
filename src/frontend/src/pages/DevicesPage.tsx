import { useState, useEffect } from 'react';
import { Plus, HardDrive, AlertTriangle, RefreshCw } from 'lucide-react';
import { DeviceCard } from '../components/DeviceCard';
import { Device } from '../types/devices';
import { devicesService } from '../services/devicesService';

// Devices management page
export function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadDevices = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await devicesService.getDevices();
      setDevices(data);
    } catch (err) {
      console.error('Failed to load devices:', err);
      setError('Failed to load devices. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDevices();
  }, []);

  const handleAddDevice = () => {
    console.log('Add device clicked');
    // TODO: Open modal to add device
  };

  const handleToggleDevice = async (deviceId: string) => {
    try {
      console.log('Start backup for device:', deviceId);
      await devicesService.triggerBackup(deviceId);
      // Reload to get updated status
      await loadDevices();
    } catch (err) {
      console.error('Failed to start backup:', err);
      setError('Failed to start backup. Please try again.');
    }
  };

  const handleViewBackups = (deviceId: string) => {
    console.log('View backups for device:', deviceId);
    // TODO: Navigate to backups page
  };

  const handleEditDevice = (deviceId: string) => {
    console.log('Edit device:', deviceId);
    // TODO: Open modal to edit device
  };

  const handleDeleteDevice = async (deviceId: string) => {
    if (!confirm('Are you sure you want to delete this device? This will also delete all its shares.')) {
      return;
    }
    
    try {
      await devicesService.deleteDevice(deviceId);
      await loadDevices();
    } catch (err) {
      console.error('Failed to delete device:', err);
      setError('Failed to delete device. Please try again.');
    }
  };

  const handleAddShare = (deviceId: string) => {
    console.log('Add share for device:', deviceId);
    // TODO: Open modal to add share
  };

  const handleToggleShare = async (deviceId: string, shareId: string) => {
    try {
      const device = devices.find(d => d.id === deviceId);
      const share = device?.shares.find(s => s.id === shareId);
      
      if (!share) return;
      
      await devicesService.updateShare(deviceId, shareId, {
        enabled: !share.enabled,
      });
      
      await loadDevices();
    } catch (err) {
      console.error('Failed to toggle share:', err);
      setError('Failed to update share. Please try again.');
    }
  };

  const handleStartShareBackup = async (deviceId: string, shareId: string) => {
    try {
      console.log('Starting backup for share:', shareId, 'on device:', deviceId);
      await devicesService.triggerShareBackup(deviceId, shareId);
      // Reload to get updated status
      await loadDevices();
    } catch (err) {
      console.error('Failed to start share backup:', err);
      setError('Failed to start backup. Please try again.');
    }
  };

  const handleViewShareBackups = (deviceId: string, shareId: string) => {
    console.log('View backups for share:', shareId, 'on device:', deviceId);
    // TODO: Navigate to share backups
  };

  const handleEditShare = (deviceId: string, shareId: string) => {
    console.log('Edit share:', shareId, 'on device:', deviceId);
    // TODO: Open modal to edit share
  };

  const handleDeleteShare = async (deviceId: string, shareId: string) => {
    if (!confirm('Are you sure you want to delete this share?')) {
      return;
    }
    
    try {
      await devicesService.deleteShare(deviceId, shareId);
      await loadDevices();
    } catch (err) {
      console.error('Failed to delete share:', err);
      setError('Failed to delete share. Please try again.');
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Devices & Shares</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage backup devices and their shared folders
          </p>
        </div>
        <button
          onClick={handleAddDevice}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-white font-medium rounded-lg hover:bg-primary/90 transition-colors shadow-sm"
        >
          <Plus className="w-4 h-4" />
          Add Device
        </button>
      </div>

      {/* Error Alert */}
      {error && (
        <div className="bg-status-error-bg border border-status-error text-status-error-fg px-4 py-3 rounded-lg">
          <div className="flex items-center">
            <AlertTriangle className="w-4 h-4 mr-2" />
            <div>
              <div className="font-semibold">Error</div>
              <div className="text-sm">{error}</div>
              <button
                onClick={loadDevices}
                className="text-sm underline mt-1"
              >
                Retry
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Loading State */}
      {loading && (
        <div className="flex items-center justify-center py-12">
          <RefreshCw className="w-6 h-6 animate-spin text-primary" />
        </div>
      )}

      {/* Devices List */}
      {!loading && devices.length > 0 && (
        <div className="space-y-4">
          {devices.map((device) => (
            <DeviceCard
              key={device.id}
              device={device}
              onToggleDevice={handleToggleDevice}
              onViewBackups={handleViewBackups}
              onEdit={handleEditDevice}
              onDelete={handleDeleteDevice}
              onAddShare={handleAddShare}
              onToggleShare={handleToggleShare}
              onStartShareBackup={handleStartShareBackup}
              onViewShareBackups={handleViewShareBackups}
              onEditShare={handleEditShare}
              onDeleteShare={handleDeleteShare}
            />
          ))}
        </div>
      )}
      
      {/* Empty State */}
      {!loading && devices.length === 0 && (
        <div className="bg-card rounded-lg shadow-sm border border-border p-12">
          <div className="text-center max-w-md mx-auto">
            <div className="inline-flex items-center justify-center w-16 h-16 bg-muted rounded-full mb-4">
              <HardDrive className="w-7 h-7 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-semibold text-foreground mb-2">No devices configured</h3>
            <p className="text-sm text-muted-foreground mb-6">
              Add your first backup device to get started. You can configure devices and their shares
              to automate your backup workflows.
            </p>
            <button
              onClick={handleAddDevice}
              className="inline-flex items-center gap-2 px-6 py-3 bg-primary text-white font-medium rounded-lg hover:bg-primary/90 transition-colors shadow-sm"
            >
              <Plus className="w-4 h-4" />
              Add Device
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
