import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Device, Share } from '../types';
import { deviceService, shareService } from '../services/deviceService';
import { ArrowLeft, RefreshCw, HardDrive } from 'lucide-react';
import { ShareList } from '../components/ShareList';
import { useNavigate } from 'react-router-dom';

export default function DeviceDetail() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const navigate = useNavigate();
  const [device, setDevice] = useState<Device | null>(null);
  const [shares, setShares] = useState<Share[]>([]);
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    if (!deviceId) return;
    try {
      setLoading(true);
      const [deviceData, sharesData] = await Promise.all([
        deviceService.getDevice(deviceId),
        shareService.listShares(deviceId),
      ]);
      setDevice(deviceData);
      setShares(sharesData);
    } catch (error) {
      console.error('Failed to load device', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [deviceId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <RefreshCw className="w-8 h-8 animate-spin text-blue-500" />
      </div>
    );
  }

  if (!device) {
    return <div className="p-6">Device not found</div>;
  }

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <button
        onClick={() => navigate('/')}
        className="flex items-center gap-2 text-blue-600 hover:text-blue-800 mb-4"
      >
        <ArrowLeft size={20} /> Back to Dashboard
      </button>

      <div className="bg-white rounded-lg shadow p-6 mb-6">
        <div className="flex justify-between items-start mb-4">
          <h1 className="text-3xl font-bold">{device.name}</h1>
          <button
            onClick={() => navigate(`/devices/${deviceId}/backups`)}
            className="inline-flex items-center px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
          >
            <HardDrive size={16} className="mr-2" />
            Browse Backups
          </button>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <span className="font-semibold">Protocol:</span> {device.protocol}
          </div>
          <div>
            <span className="font-semibold">Host:</span> {device.host}:{device.port}
          </div>
          <div>
            <span className="font-semibold">Username:</span> {device.username}
          </div>
          <div>
            <span className="font-semibold">Schedule:</span>{' '}
            {device.schedule?.cronExpression || 'None'}
          </div>
          <div>
            <span className="font-semibold">WOL Enabled:</span>{' '}
            {device.wakeOnLanEnabled ? 'Yes' : 'No'}
          </div>
          <div>
            <span className="font-semibold">Last Updated:</span>{' '}
            {new Date(device.updatedAt).toLocaleString()}
          </div>
        </div>
      </div>

      {deviceId && <ShareList deviceId={deviceId} shares={shares} onShareUpdated={loadData} />}
    </div>
  );
}
