import { useState } from 'react';
import { DeviceCreateDto, ProtocolType, Device } from '../types';
import { deviceService } from '../services/deviceService';
import { ErrorDisplay } from './ErrorDisplay';

interface DeviceFormProps {
  device?: Device;
  onSuccess: () => void;
  onCancel: () => void;
}

export default function DeviceForm({ device, onSuccess, onCancel }: DeviceFormProps) {
  const [formData, setFormData] = useState<DeviceCreateDto>({
    name: device?.name || '',
    protocol: device?.protocol || ProtocolType.SMB,
    host: device?.host || '',
    port: device?.port || 445,
    username: device?.username || '',
    password: device?.password || '',
    wakeOnLanEnabled: device?.wakeOnLanEnabled || false,
    wakeOnLanMacAddress: device?.wakeOnLanMacAddress,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<any>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      if (device) {
        await deviceService.updateDevice(device.id, formData);
      } else {
        await deviceService.createDevice(formData);
      }
      onSuccess();
    } catch (err: any) {
      setError(err);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <h2 className="text-2xl font-bold mb-4">
        {device ? 'Edit Device' : 'Create Device'}
      </h2>
      <form onSubmit={handleSubmit} className="space-y-4">
        {error && <ErrorDisplay error={error} />}

        <div>
        <label className="block text-sm font-medium mb-1">Name</label>
        <input
          type="text"
          required
          value={formData.name}
          onChange={(e) => setFormData({ ...formData, name: e.target.value })}
          className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">Protocol</label>
        <select
          value={formData.protocol}
          onChange={(e) => setFormData({ ...formData, protocol: e.target.value as ProtocolType })}
          className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value={ProtocolType.SMB}>SMB</option>
          <option value={ProtocolType.SSH}>SSH</option>
          <option value={ProtocolType.Rsync}>Rsync</option>
        </select>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div>
          <label className="block text-sm font-medium mb-1">Host</label>
          <input
            type="text"
            required
            value={formData.host}
            onChange={(e) => setFormData({ ...formData, host: e.target.value })}
            className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="192.168.1.100"
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Port</label>
          <input
            type="number"
            required
            value={formData.port}
            onChange={(e) => setFormData({ ...formData, port: parseInt(e.target.value, 10) || 0 })}
            className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
          />        
        </div>
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">Username</label>
        <input
          type="text"
          required
          value={formData.username}
          onChange={(e) => setFormData({ ...formData, username: e.target.value })}
          className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">
          Password {device && <span className="text-gray-500 text-xs">(leave empty to keep existing)</span>}
        </label>
        <input
          type="password"
          required={!device}
          value={formData.password}
          onChange={(e) => setFormData({ ...formData, password: e.target.value })}
          className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div className="flex items-center">
        <input
          type="checkbox"
          id="wol"
          checked={formData.wakeOnLanEnabled}
          onChange={(e) => setFormData({ ...formData, wakeOnLanEnabled: e.target.checked })}
          className="mr-2"
        />
        <label htmlFor="wol" className="text-sm">Enable Wake-on-LAN</label>
      </div>

      {formData.wakeOnLanEnabled && (
        <div>
          <label className="block text-sm font-medium mb-1">MAC Address</label>
          <input
            type="text"
            value={formData.wakeOnLanMacAddress || ''}
            onChange={(e) => setFormData({ ...formData, wakeOnLanMacAddress: e.target.value })}
            className="w-full px-3 py-2 border rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="00:11:22:33:44:55"
          />
        </div>
      )}

      <div className="flex gap-2 justify-end mt-6">
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 border rounded hover:bg-gray-50"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={submitting}
          className="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600 disabled:opacity-50"
        >
          {submitting ? (device ? 'Updating...' : 'Creating...') : (device ? 'Update Device' : 'Create Device')}
        </button>
      </div>
    </form>
    </div>
  );
}
