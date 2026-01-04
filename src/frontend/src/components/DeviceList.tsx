import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Trash2, Edit, Play, Plus } from 'lucide-react';
import { Device } from '../types';
import { deviceService } from '../services/deviceService';
import { apiClient } from '../services/api';
import { ErrorDisplay } from './ErrorDisplay';
import { SuccessNotification } from './SuccessNotification';
import DeviceForm from './DeviceForm';

interface DeviceListProps {
  devices: Device[];
  onDeviceUpdated: () => void;
}

export function DeviceList({ devices, onDeviceUpdated }: DeviceListProps) {
  const navigate = useNavigate();
  const [showForm, setShowForm] = useState(false);
  const [editingDevice, setEditingDevice] = useState<Device | null>(null);
  const [triggeringBackup, setTriggeringBackup] = useState<string | null>(null);
  const [backupError, setBackupError] = useState<any>(null);
  const [deleteError, setDeleteError] = useState<any>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const handleDelete = async (id: string) => {
    if (!confirm('Delete this device?')) return;
    setDeleteError(null);
    try {
      await deviceService.deleteDevice(id);
      setSuccessMessage('Device deleted successfully');
      onDeviceUpdated();
    } catch (err: any) {
      setDeleteError(err);
    }
  };

  const handleTriggerBackup = async (deviceId: string) => {
    setTriggeringBackup(deviceId);
    setBackupError(null);
    try {
      await apiClient.post('/api/backup-jobs', {
        deviceId: deviceId,
        shareId: null
      });
      setSuccessMessage('Backup triggered successfully');
      // Don't call onDeviceUpdated() here - it causes the component to remount
      // The backup jobs list will auto-refresh anyway
    } catch (err: any) {
      setBackupError(err);
    } finally {
      setTriggeringBackup(null);
    }
  };

  const handleEdit = (device: Device) => {
    setEditingDevice(device);
    setShowForm(true);
  };

  const handleFormClose = () => {
    setShowForm(false);
    setEditingDevice(null);
    onDeviceUpdated();
  };

  const handleFormCancel = () => {
    setShowForm(false);
    setEditingDevice(null);
  };

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <h2 className="text-2xl font-bold">Devices</h2>
        <button
          type="button"
          onClick={() => setShowForm(true)}
          className="bg-blue-500 text-white px-4 py-2 rounded hover:bg-blue-600 flex items-center gap-2"
        >
          <Plus size={20} /> Add Device
        </button>
      </div>

      {showForm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 max-w-2xl w-full max-h-[90vh] overflow-y-auto">
            <DeviceForm
              device={editingDevice || undefined}
              onSuccess={handleFormClose}
              onCancel={handleFormCancel}
            />
          </div>
        </div>
      )}

      {successMessage && (
        <SuccessNotification
          message={successMessage}
          onClose={() => setSuccessMessage(null)}
        />
      )}

      {backupError && (
        <div className="mb-4">
          <ErrorDisplay error={backupError} />
        </div>
      )}

      {deleteError && (
        <div className="mb-4">
          <ErrorDisplay error={deleteError} />
        </div>
      )}

      <div className="bg-white rounded-lg shadow">
        <table className="w-full">
          <thead>
            <tr className="border-b bg-gray-50">
              <th className="text-left p-3">Name</th>
              <th className="text-left p-3">Protocol</th>
              <th className="text-left p-3">Host</th>
              <th className="text-left p-3">Schedule</th>
              <th className="text-right p-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {devices.map((device) => (
              <tr key={device.id} className="border-b hover:bg-gray-50">
                <td className="p-3">
                  <button
                    onClick={() => navigate(`/devices/${device.id}`)}
                    className="font-medium text-blue-600 hover:text-blue-800 hover:underline text-left"
                  >
                    {device.name}
                  </button>
                </td>
                <td className="p-3">{device.protocol}</td>
                <td className="p-3">{device.host}:{device.port}</td>
                <td className="p-3">{device.schedule?.cronExpression || 'None'}</td>
                <td className="p-3">
                  <div className="flex gap-2 justify-end">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.preventDefault();
                        handleTriggerBackup(device.id);
                      }}
                      disabled={triggeringBackup === device.id}
                      className="text-green-600 hover:text-green-800 disabled:opacity-50"
                      title="Trigger Backup"
                    >
                      <Play size={18} />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleEdit(device)}
                      className="text-blue-600 hover:text-blue-800"
                      title="Edit"
                    >
                      <Edit size={18} />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDelete(device.id)}
                      className="text-red-600 hover:text-red-800"
                      title="Delete"
                    >
                      <Trash2 size={18} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {devices.length === 0 && (
          <div className="text-center py-8 text-gray-500">
            No devices configured. Click "Add Device" to get started.
          </div>
        )}
      </div>
    </div>
  );
}
