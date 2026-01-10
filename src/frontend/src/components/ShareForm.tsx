import { useState } from 'react';
import { Share, ShareCreateDto } from '../types';
import { shareService } from '../services/deviceService';
import { ErrorDisplay } from './ErrorDisplay';

interface ShareFormProps {
  deviceId: string;
  share?: Share | null;
  onClose: () => void;
}

export function ShareForm({ deviceId, share, onClose }: ShareFormProps) {
  const [formData, setFormData] = useState<ShareCreateDto>({
    name: share?.name || '',
    path: share?.path || '',
    enabled: share?.enabled ?? true,
    schedule: share?.schedule || undefined,
    retentionPolicy: share?.retentionPolicy || undefined,
  });

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      if (share) {
        await shareService.updateShare(deviceId, share.id, formData);
      } else {
        await shareService.createShare(deviceId, formData);
      }
      onClose();
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <h2 className="text-2xl font-bold mb-4">
        {share ? 'Edit Share' : 'Add Share'}
      </h2>
      {error !== null && <ErrorDisplay error={error as Error | string} />}
      <form onSubmit={handleSubmit} className="mt-4">
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-1">Name</label>
            <input
              type="text"
              value={formData.name}
              onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              className="w-full border rounded px-3 py-2"
              required
            />
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Path</label>
            <input
              type="text"
              value={formData.path}
              onChange={(e) => setFormData({ ...formData, path: e.target.value })}
              className="w-full border rounded px-3 py-2"
              placeholder="/path/to/share"
              required
            />
          </div>

          <div>
            <label className="flex items-center">
              <input
                type="checkbox"
                checked={formData.enabled}
                onChange={(e) => setFormData({ ...formData, enabled: e.target.checked })}
                className="mr-2"
              />
              <span className="text-sm font-medium">Enabled</span>
            </label>
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">
              Schedule (Cron Expression) - Optional
            </label>
            <input
              type="text"
              value={formData.schedule?.cronExpression || ''}
              onChange={(e) =>
                setFormData({
                  ...formData,
                  schedule: e.target.value ? { cronExpression: e.target.value } : undefined,
                })
              }
              className="w-full border rounded px-3 py-2"
              placeholder="0 0 2 * * ? (leave empty to inherit from device)"
            />
            <p className="text-xs text-gray-500 mt-1">
              Format: seconds minutes hours day month weekday
            </p>
          </div>
        </div>

        <div className="flex gap-3 mt-6">
          <button
            type="submit"
            disabled={saving}
            className="bg-blue-500 text-white px-4 py-2 rounded hover:bg-blue-600 disabled:opacity-50"
          >
            {saving ? 'Saving...' : 'Save'}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="bg-gray-300 text-gray-700 px-4 py-2 rounded hover:bg-gray-400"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
