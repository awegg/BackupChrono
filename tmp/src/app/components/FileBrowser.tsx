import { useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from './Card';
import { Button } from './Button';
import { Select } from './Input';
import { 
  Folder, 
  File, 
  ChevronRight, 
  Home, 
  Calendar, 
  Download,
  Clock,
  HardDrive
} from 'lucide-react';

interface FileNode {
  name: string;
  type: 'file' | 'folder';
  size?: string;
  modified?: string;
  children?: FileNode[];
}

interface Snapshot {
  id: string;
  timestamp: string;
  device: string;
  size: string;
}

export function FileBrowser() {
  const [selectedSnapshot, setSelectedSnapshot] = useState('snapshot-1');
  const [currentPath, setCurrentPath] = useState<string[]>([]);
  const [selectedFiles, setSelectedFiles] = useState<Set<string>>(new Set());

  const snapshots: Snapshot[] = [
    { id: 'snapshot-1', timestamp: '2026-01-04 09:00', device: 'prod-server-01', size: '45.2 GB' },
    { id: 'snapshot-2', timestamp: '2026-01-03 09:00', device: 'prod-server-01', size: '45.1 GB' },
    { id: 'snapshot-3', timestamp: '2026-01-02 09:00', device: 'prod-server-01', size: '44.8 GB' },
    { id: 'snapshot-4', timestamp: '2026-01-01 09:00', device: 'prod-server-01', size: '44.5 GB' },
  ];

  // Mock file system structure
  const fileSystem: FileNode = {
    name: 'root',
    type: 'folder',
    children: [
      {
        name: 'var',
        type: 'folder',
        children: [
          {
            name: 'www',
            type: 'folder',
            children: [
              {
                name: 'html',
                type: 'folder',
                children: [
                  {
                    name: 'wp-content',
                    type: 'folder',
                    children: [
                      {
                        name: 'uploads',
                        type: 'folder',
                        children: [
                          { name: 'image-001.jpg', type: 'file', size: '2.4 MB', modified: '2026-01-04 08:30' },
                          { name: 'image-002.jpg', type: 'file', size: '1.8 MB', modified: '2026-01-04 08:32' },
                          { name: 'document.pdf', type: 'file', size: '5.2 MB', modified: '2026-01-03 14:20' },
                        ]
                      },
                      {
                        name: 'themes',
                        type: 'folder',
                        children: [
                          { name: 'style.css', type: 'file', size: '145 KB', modified: '2026-01-02 10:15' },
                          { name: 'functions.php', type: 'file', size: '32 KB', modified: '2026-01-02 10:15' },
                        ]
                      }
                    ]
                  },
                  { name: 'index.php', type: 'file', size: '12 KB', modified: '2025-12-15 09:00' },
                  { name: 'wp-config.php', type: 'file', size: '3 KB', modified: '2025-12-15 09:00' },
                ]
              }
            ]
          },
          {
            name: 'log',
            type: 'folder',
            children: [
              { name: 'apache2.log', type: 'file', size: '234 MB', modified: '2026-01-04 09:15' },
              { name: 'error.log', type: 'file', size: '12 MB', modified: '2026-01-04 09:14' },
            ]
          }
        ]
      },
      {
        name: 'etc',
        type: 'folder',
        children: [
          { name: 'nginx.conf', type: 'file', size: '8 KB', modified: '2025-11-20 14:30' },
          { name: 'hosts', type: 'file', size: '1 KB', modified: '2025-11-20 14:30' },
        ]
      }
    ]
  };

  const getCurrentFolder = (): FileNode => {
    let folder = fileSystem;
    for (const pathSegment of currentPath) {
      const child = folder.children?.find(c => c.name === pathSegment);
      if (child && child.type === 'folder') {
        folder = child;
      }
    }
    return folder;
  };

  const navigateToFolder = (folderName: string) => {
    setCurrentPath([...currentPath, folderName]);
  };

  const navigateUp = () => {
    setCurrentPath(currentPath.slice(0, -1));
  };

  const navigateToIndex = (index: number) => {
    setCurrentPath(currentPath.slice(0, index + 1));
  };

  const toggleFileSelection = (fileName: string) => {
    const newSelection = new Set(selectedFiles);
    const fullPath = [...currentPath, fileName].join('/');
    
    if (newSelection.has(fullPath)) {
      newSelection.delete(fullPath);
    } else {
      newSelection.add(fullPath);
    }
    setSelectedFiles(newSelection);
  };

  const currentFolder = getCurrentFolder();
  const currentSnapshot = snapshots.find(s => s.id === selectedSnapshot);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1>Backup File Browser</h1>
        <p className="text-muted-foreground mt-1">Browse and restore files from backup snapshots</p>
      </div>

      {/* Snapshot Selector */}
      <Card>
        <CardHeader>
          <CardTitle>Select Backup Snapshot</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="Backup Snapshot"
              options={snapshots.map(s => ({
                value: s.id,
                label: `${s.timestamp} • ${s.size}`
              }))}
              value={selectedSnapshot}
              onChange={(e) => setSelectedSnapshot(e.target.value)}
            />
          </div>

          {currentSnapshot && (
            <div className="flex gap-6 p-4 bg-muted/50 rounded-lg">
              <div className="flex items-center gap-2">
                <Calendar className="w-4 h-4 text-muted-foreground" />
                <div>
                  <div className="text-muted-foreground">Timestamp</div>
                  <div className="font-[var(--font-mono)]">{currentSnapshot.timestamp}</div>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <HardDrive className="w-4 h-4 text-muted-foreground" />
                <div>
                  <div className="text-muted-foreground">Device</div>
                  <div className="font-medium">{currentSnapshot.device}</div>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <Download className="w-4 h-4 text-muted-foreground" />
                <div>
                  <div className="text-muted-foreground">Total Size</div>
                  <div className="font-[var(--font-mono)]">{currentSnapshot.size}</div>
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* File Browser */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Browse Files</CardTitle>
            <Button 
              variant="primary" 
              size="sm"
              disabled={selectedFiles.size === 0}
            >
              <Download className="w-4 h-4" />
              Restore {selectedFiles.size > 0 ? `(${selectedFiles.size})` : ''}
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Breadcrumb Navigation */}
          <div className="flex items-center gap-2 p-3 bg-muted/50 rounded-lg overflow-x-auto">
            <button
              onClick={() => setCurrentPath([])}
              className="flex items-center gap-1 px-2 py-1 rounded hover:bg-background transition-colors"
            >
              <Home className="w-4 h-4" />
              <span className="font-[var(--font-mono)]">root</span>
            </button>
            {currentPath.map((segment, index) => (
              <div key={index} className="flex items-center gap-2">
                <ChevronRight className="w-4 h-4 text-muted-foreground" />
                <button
                  onClick={() => navigateToIndex(index)}
                  className="px-2 py-1 rounded hover:bg-background transition-colors font-[var(--font-mono)]"
                >
                  {segment}
                </button>
              </div>
            ))}
          </div>

          {/* File List */}
          <div className="border border-border rounded-lg overflow-hidden">
            <div className="bg-muted px-4 py-3 border-b border-border">
              <div className="grid grid-cols-12 gap-4">
                <div className="col-span-1"></div>
                <div className="col-span-5 text-muted-foreground">Name</div>
                <div className="col-span-2 text-muted-foreground">Size</div>
                <div className="col-span-4 text-muted-foreground">Modified</div>
              </div>
            </div>

            <div className="divide-y divide-border">
              {currentPath.length > 0 && (
                <button
                  onClick={navigateUp}
                  className="w-full px-4 py-3 hover:bg-accent/50 transition-colors"
                >
                  <div className="grid grid-cols-12 gap-4 items-center">
                    <div className="col-span-1"></div>
                    <div className="col-span-5 flex items-center gap-2">
                      <Folder className="w-5 h-5 text-warning" />
                      <span className="font-[var(--font-mono)]">..</span>
                    </div>
                    <div className="col-span-2"></div>
                    <div className="col-span-4"></div>
                  </div>
                </button>
              )}

              {currentFolder.children?.map((node, index) => {
                const fullPath = [...currentPath, node.name].join('/');
                const isSelected = selectedFiles.has(fullPath);

                return (
                  <div
                    key={index}
                    className="px-4 py-3 hover:bg-accent/50 transition-colors"
                  >
                    <div className="grid grid-cols-12 gap-4 items-center">
                      <div className="col-span-1">
                        {node.type === 'file' && (
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => toggleFileSelection(node.name)}
                            className="w-4 h-4 rounded border-border"
                          />
                        )}
                      </div>
                      <div className="col-span-5">
                        {node.type === 'folder' ? (
                          <button
                            onClick={() => navigateToFolder(node.name)}
                            className="flex items-center gap-2 hover:text-primary transition-colors"
                          >
                            <Folder className="w-5 h-5 text-warning" />
                            <span className="font-[var(--font-mono)]">{node.name}</span>
                          </button>
                        ) : (
                          <div className="flex items-center gap-2">
                            <File className="w-5 h-5 text-muted-foreground" />
                            <span className="font-[var(--font-mono)]">{node.name}</span>
                          </div>
                        )}
                      </div>
                      <div className="col-span-2">
                        {node.size && (
                          <span className="font-[var(--font-mono)]">{node.size}</span>
                        )}
                      </div>
                      <div className="col-span-4 flex items-center gap-2">
                        {node.modified && (
                          <>
                            <Clock className="w-4 h-4 text-muted-foreground" />
                            <span className="font-[var(--font-mono)]">{node.modified}</span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}

              {currentFolder.children?.length === 0 && (
                <div className="px-4 py-8 text-center text-muted-foreground">
                  This folder is empty
                </div>
              )}
            </div>
          </div>

          {/* Selection Summary */}
          {selectedFiles.size > 0 && (
            <div className="p-4 bg-primary/10 border border-primary/20 rounded-lg">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium">{selectedFiles.size} file{selectedFiles.size !== 1 ? 's' : ''} selected for restoration</p>
                  <p className="text-muted-foreground mt-1">Files will be restored to their original locations</p>
                </div>
                <Button variant="secondary" size="sm" onClick={() => setSelectedFiles(new Set())}>
                  Clear Selection
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
