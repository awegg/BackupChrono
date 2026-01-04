import { useState } from 'react';
import { Trash2, Edit, Plus, ToggleLeft, ToggleRight } from 'lucide-react';
import { Share } from '../types';
import { shareService } from '../services/deviceService';
import { ShareForm } from './ShareForm';

interface ShareListProps {
  deviceId: string;
  shares: Share[];
  onShareUpdated: () => void;
}

export function ShareList({ deviceId, shares, onShareUpdated }: ShareListProps) {
  const [showForm, setShowForm] = useState(false);
  const [editingShare, setEditingShare] = useState<Share | null>(null);

  const handleDelete = async (shareId: string) => {
    if (!confirm('Delete this share?')) return;
    try {
      await shareService.deleteShare(deviceId, shareId);
      onShareUpdated();
    } catch (error) {
      alert('Failed to delete share');
    }
  };

  const handleToggleEnabled = async (share: Share) => {
    try {
      await shareService.updateShare(deviceId, share.id, {
        ...share,
        enabled: !share.enabled
      });
      onShareUpdated();
    } catch (error) {
      alert('Failed to toggle share');
    }
  };

  const handleEdit = (share: Share) => {
    setEditingShare(share);
    setShowForm(true);
  };

  const handleFormClose = () => {
    setShowForm(false);
    setEditingShare(null);
    onShareUpdated();
  };

  return (
    <div className="mt-6">
      <div className="flex justify-between items-center mb-4">
        <h3 className="text-xl font-bold">Shares</h3>
        <button
          onClick={() => setShowForm(true)}
          className="bg-blue-500 text-white px-3 py-1.5 rounded hover:bg-blue-600 flex items-center gap-2 text-sm"
        >
          <Plus size={16} /> Add Share
        </button>
      </div>

      {showForm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 max-w-xl w-full max-h-[90vh] overflow-y-auto">
            <ShareForm
              deviceId={deviceId}
              share={editingShare}
              onClose={handleFormClose}
            />
          </div>
        </div>
      )}

      <div className="bg-white rounded-lg shadow">
        <table className="w-full">
          <thead>
            <tr className="border-b bg-gray-50">
              <th className="text-left p-3">Name</th>
              <th className="text-left p-3">Path</th>
              <th className="text-left p-3">Schedule</th>
              <th className="text-left p-3">Enabled</th>
              <th className="text-right p-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {shares.map((share) => (
              <tr key={share.id} className="border-b hover:bg-gray-50">
                <td className="p-3 font-medium">{share.name}</td>
                <td className="p-3">{share.path}</td>
                <td className="p-3">{share.schedule?.cronExpression || 'Inherit'}</td>
                <td className="p-3">
                  <button
                    onClick={() => handleToggleEnabled(share)}
                    className={share.enabled ? 'text-green-600' : 'text-gray-400'}
                    title={share.enabled ? 'Enabled' : 'Disabled'}
                  >
                    {share.enabled ? <ToggleRight size={24} /> : <ToggleLeft size={24} />}
                  </button>
                </td>
                <td className="p-3">
                  <div className="flex gap-2 justify-end">
                    <button
                      onClick={() => handleEdit(share)}
                      className="text-blue-600 hover:text-blue-800"
                      title="Edit"
                    >
                      <Edit size={18} />
                    </button>
                    <button
                      onClick={() => handleDelete(share.id)}
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
        {shares.length === 0 && (
          <div className="text-center py-8 text-gray-500">
            No shares configured for this device.
          </div>
        )}
      </div>
    </div>
  );
}
