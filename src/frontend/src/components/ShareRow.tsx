﻿import { Folder, Play, Settings, Trash2 } from 'lucide-react';
import { Share } from '../types/devices';
import { formatTimestamp } from '../utils/timeFormat';

interface ShareRowProps {
  share: Share;
  onToggle: (shareId: string) => void;
  onStartBackup: (shareId: string) => void;
  onViewBackups: (shareId: string) => void;
  onEdit: (shareId: string) => void;
  onDelete: (shareId: string) => void;
}

export function ShareRow({ share, onToggle, onStartBackup, onViewBackups: _onViewBackups, onEdit, onDelete }: ShareRowProps) {
  return (
    <div className="flex items-center gap-4 py-3 px-4 hover:bg-muted/30 rounded-md transition-colors group">
      <Folder className="w-4 h-4 text-muted-foreground flex-shrink-0" />
      
      <div className="flex-1 min-w-0">
        <div className="font-medium text-sm text-foreground">{share.name}</div>
        <div className="text-xs font-mono text-muted-foreground truncate">{share.path}</div>
      </div>

      <div className="text-sm text-muted-foreground whitespace-nowrap">
        {share.lastBackup ? formatTimestamp(share.lastBackup) : 'No backups yet'}
      </div>

      <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
        <button
          onClick={() => onStartBackup(share.id)}
          className="p-1.5 hover:bg-primary/10 rounded-md transition-colors"
          title="Start backup"
        >
          <Play className="w-4 h-4 text-primary" />
        </button>
        <button
          onClick={() => onEdit(share.id)}
          className="p-1.5 hover:bg-muted rounded-md transition-colors"
          title="Edit share"
        >
          <Settings className="w-4 h-4 text-muted-foreground" />
        </button>
        <button
          onClick={() => onDelete(share.id)}
          className="p-1.5 hover:bg-status-error-bg rounded-md transition-colors"
          title="Delete share"
        >
          <Trash2 className="w-4 h-4 text-status-error" />
        </button>
      </div>

      <button
        onClick={() => onToggle(share.id)}
        className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
          share.enabled ? 'bg-status-success' : 'bg-muted'
        }`}
        title={share.enabled ? 'Enabled' : 'Disabled'}
      >
        <span
          className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
            share.enabled ? 'translate-x-6' : 'translate-x-1'
          }`}
        />
      </button>
    </div>
  );
}
