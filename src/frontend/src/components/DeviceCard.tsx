import { useState } from 'react';
import { ChevronDown, Server, Play, FolderOpen, Edit, Trash2, Plus } from 'lucide-react';
import { Device, DeviceStatus } from '../types/devices';
import { ShareRow } from './ShareRow';
import { formatTimestamp } from '../utils/timeFormat';

interface DeviceCardProps {
  device: Device;
  onStartDeviceBackup: (deviceId: string) => void;
  onViewBackups: (deviceId: string) => void;
  onEdit: (deviceId: string) => void;
  onDelete: (deviceId: string) => void;
  onAddShare: (deviceId: string) => void;
  onToggleShare: (deviceId: string, shareId: string) => void;
  onStartShareBackup: (deviceId: string, shareId: string) => void;
  onViewShareBackups: (deviceId: string, shareId: string) => void;
  onEditShare: (deviceId: string, shareId: string) => void;
  onDeleteShare: (deviceId: string, shareId: string) => void;
}

export function DeviceCard({
  device,
  onStartDeviceBackup,
  onViewBackups,
  onEdit,
  onDelete,
  onAddShare,
  onToggleShare,
  onStartShareBackup,
  onViewShareBackups,
  onEditShare,
  onDeleteShare,
}: DeviceCardProps) {
  const [isExpanded, setIsExpanded] = useState(false);

  return (
    <div className="bg-card rounded-lg shadow-sm border border-border overflow-hidden">
      {/* Device Header */}
      <div
        className="flex items-center gap-4 p-4 hover:bg-muted/50 transition-colors cursor-pointer"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <button
          className="p-1 hover:bg-muted rounded-md transition-colors"
          onClick={(e) => {
            e.stopPropagation();
            setIsExpanded(!isExpanded);
          }}
        >
          <ChevronDown
            className={`w-4 h-4 text-muted-foreground transition-transform ${
              isExpanded ? 'rotate-0' : '-rotate-90'
            }`}
          />
        </button>

        <Server className="w-5 h-5 text-primary flex-shrink-0" />

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3">
            <h3 className="text-lg font-semibold text-foreground">{device.name}</h3>
            <span
              className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium ${
                device.status === DeviceStatus.Active
                  ? 'bg-status-success-bg text-status-success-fg'
                  : device.status === DeviceStatus.Offline
                  ? 'bg-status-error-bg text-status-error-fg'
                  : 'bg-muted text-muted-foreground'
              }`}
            >
              <span
                className={`w-1.5 h-1.5 rounded-full ${
                  device.status === DeviceStatus.Active 
                    ? 'bg-status-success' 
                    : device.status === DeviceStatus.Offline
                    ? 'bg-status-error'
                    : 'bg-muted-foreground'
                }`}
              />
              {device.status}
            </span>
          </div>
          <div className="flex items-center gap-4 mt-1 text-sm text-muted-foreground">
            <span className="font-mono">{device.host}</span>
            <span className="px-2 py-0.5 bg-muted rounded text-xs font-medium">{device.protocol}</span>
            {device.lastBackup && <span>Last backup: {formatTimestamp(device.lastBackup)}</span>}
            {!device.lastBackup && <span className="text-muted-foreground/70">No backups yet</span>}
          </div>
        </div>

        <div className="flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
          <button
            onClick={() => onStartDeviceBackup(device.id)}
            className="p-2 hover:bg-muted rounded-md transition-colors"
            title="Start backup"
          >
            <Play className="w-4 h-4 text-muted-foreground" />
          </button>
          <button
            onClick={() => onViewBackups(device.id)}
            className="p-2 hover:bg-muted rounded-md transition-colors"
            title="View backups"
          >
            <FolderOpen className="w-4 h-4 text-muted-foreground" />
          </button>
          <button
            onClick={() => onEdit(device.id)}
            className="p-2 hover:bg-muted rounded-md transition-colors"
            title="Edit device"
          >
            <Edit className="w-4 h-4 text-muted-foreground" />
          </button>
          <button
            onClick={() => onDelete(device.id)}
            className="p-2 hover:bg-status-error-bg rounded-md transition-colors"
            title="Delete device"
          >
            <Trash2 className="w-4 h-4 text-status-error" />
          </button>
        </div>
      </div>

      {/* Shares Section (Expanded) */}
      {isExpanded && (
        <div className="border-t border-border bg-muted/20">
          <div className="px-4 py-3 flex items-center justify-between border-b border-border">
            <h4 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
              Shares ({device.shares.length})
            </h4>
            <button
              onClick={() => onAddShare(device.id)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-primary hover:bg-primary/10 rounded-md transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Share
            </button>
          </div>

          <div className="px-4 py-2 space-y-1">
            {device.shares.map((share) => (
              <ShareRow
                key={share.id}
                share={share}
                onToggle={(shareId) => onToggleShare(device.id, shareId)}
                onStartBackup={(shareId) => onStartShareBackup(device.id, shareId)}
                onViewBackups={(shareId) => onViewShareBackups(device.id, shareId)}
                onEdit={(shareId) => onEditShare(device.id, shareId)}
                onDelete={(shareId) => onDeleteShare(device.id, shareId)}
              />
            ))}

            {device.shares.length === 0 && (
              <div className="py-8 text-center text-muted-foreground text-sm">
                No shares configured for this device
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
