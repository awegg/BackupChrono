import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, HardDrive, AlertTriangle, RefreshCw } from 'lucide-react';
import { DeviceCard } from '../components/DeviceCard';
import { AddDeviceDialog } from '../components/AddDeviceDialog';
import { AddShareDialog } from '../components/AddShareDialog';
import { Device, Share } from '../types/devices';
import { devicesService } from '../services/devicesService';

// Devices management page
export function DevicesPage() {
  const navigate = useNavigate();
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [editingDeviceId, setEditingDeviceId] = useState<string | undefined>();
  const [showAddShareDialog, setShowAddShareDialog] = useState(false);
  const [selectedDeviceForShare, setSelectedDeviceForShare] = useState<Device | null>(null);
  const [editingShare, setEditingShare] = useState<Share | null>(null);

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
    setEditingDeviceId(undefined);
    setShowAddDialog(true);
  };

  const handleStartDeviceBackup = async (deviceId: string) => {
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
    navigate(`/devices/${deviceId}/backups`);
  };

  const handleEditDevice = (deviceId: string) => {
    setEditingDeviceId(deviceId);
    setShowAddDialog(true);
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
    const device = devices.find(d => d.id === deviceId);
    if (device) {
      setSelectedDeviceForShare(device);
      setShowAddShareDialog(true);
    }
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
    navigate(`/devices/${deviceId}/backups?shareId=${encodeURIComponent(shareId)}`);
  };

  const handleEditShare = (deviceId: string, shareId: string) => {
    const device = devices.find(d => d.id === deviceId);
    const share = device?.shares.find((s: Share) => s.id === shareId);
    if (device && share) {
      setSelectedDeviceForShare(device);
      setEditingShare(share);
      setShowAddShareDialog(true);
    }
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
              onStartDeviceBackup={handleStartDeviceBackup}
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

      <AddDeviceDialog
        open={showAddDialog}
        onClose={() => {
          setShowAddDialog(false);
          setEditingDeviceId(undefined);
        }}
        onCreated={loadDevices}
        editingDeviceId={editingDeviceId}
      />

      <AddShareDialog
        open={showAddShareDialog}
        onClose={() => {
          setShowAddShareDialog(false);
          setSelectedDeviceForShare(null);
          setEditingShare(null);
        }}
        device={selectedDeviceForShare}
        onCreated={loadDevices}
        editingShare={editingShare}
      />
    </div>
  );
}
