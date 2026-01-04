import { useState } from 'react';
import { Button } from './components/Button';
import { Card, CardHeader, CardTitle, CardContent } from './components/Card';
import { StatusBadge } from './components/StatusBadge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './components/Table';
import { Input, Select } from './components/Input';
import { ProgressBar } from './components/ProgressBar';
import { MonitoringDashboard } from './components/MonitoringDashboard';
import { FileBrowser } from './components/FileBrowser';
import { DeduplicationTracker } from './components/DeduplicationTracker';
import { AlertConfiguration } from './components/AlertConfiguration';
import { DeviceHealth } from './components/DeviceHealth';
import { Server, Database, HardDrive, RefreshCw, Settings, FolderOpen, BarChart3, Activity, Bell, Cpu } from 'lucide-react';

type View = 'design-system' | 'monitoring' | 'file-browser' | 'deduplication' | 'alerts' | 'device-health';

export default function App() {
  const [darkMode, setDarkMode] = useState(false);
  const [currentView, setCurrentView] = useState<View>('monitoring');

  const toggleDarkMode = () => {
    setDarkMode(!darkMode);
    document.documentElement.classList.toggle('dark');
  };

  // Mock backup data for design system view
  const backupData = [
    { id: 1, device: 'prod-server-01', path: '/var/www/html', status: 'success', lastBackup: '2026-01-04 08:30', size: '45.2 GB', files: '128,394' },
    { id: 2, device: 'nas-storage', path: '/mnt/data/documents', status: 'success', lastBackup: '2026-01-04 07:15', size: '234.8 GB', files: '1,245,678' },
    { id: 3, device: 'workstation-05', path: 'C:\\Users\\Admin', status: 'warning', lastBackup: '2026-01-03 22:45', size: '89.3 GB', files: '56,234' },
    { id: 4, device: 'db-server-02', path: '/var/lib/postgresql', status: 'error', lastBackup: '2026-01-02 15:30', size: '156.7 GB', files: '23,456' },
    { id: 5, device: 'mail-server', path: '/var/mail', status: 'success', lastBackup: '2026-01-04 09:00', size: '67.1 GB', files: '89,123' },
  ];

  const renderContent = () => {
    switch (currentView) {
      case 'monitoring':
        return <MonitoringDashboard />;
      case 'file-browser':
        return <FileBrowser />;
      case 'deduplication':
        return <DeduplicationTracker />;
      case 'design-system':
        return <DesignSystemView backupData={backupData} />;
      case 'alerts':
        return <AlertConfiguration />;
      case 'device-health':
        return <DeviceHealth />;
      default:
        return <MonitoringDashboard />;
    }
  };

  return (
    <div className="min-h-screen flex bg-background">
      {/* Sidebar */}
      <aside className="w-64 bg-sidebar border-r border-sidebar-border p-4 flex flex-col">
        <div className="mb-8">
          <h1 className="text-sidebar-foreground mb-1">BackupChrono</h1>
          <p className="text-sidebar-foreground/70">Design System v1.0</p>
        </div>
        
        <nav className="flex-1 space-y-1">
          <button 
            onClick={() => setCurrentView('monitoring')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'monitoring' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <Activity className="w-5 h-5" />
            <span>Live Monitoring</span>
          </button>
          <button 
            onClick={() => setCurrentView('file-browser')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'file-browser' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <FolderOpen className="w-5 h-5" />
            <span>File Browser</span>
          </button>
          <button 
            onClick={() => setCurrentView('deduplication')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'deduplication' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <BarChart3 className="w-5 h-5" />
            <span>Deduplication</span>
          </button>
          <button 
            onClick={() => setCurrentView('device-health')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'device-health' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <Cpu className="w-5 h-5" />
            <span>Device Health</span>
          </button>
          <button 
            onClick={() => setCurrentView('alerts')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'alerts' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <Bell className="w-5 h-5" />
            <span>Alerts</span>
          </button>
          
          <div className="h-px bg-sidebar-border my-2"></div>
          
          <button 
            onClick={() => setCurrentView('design-system')}
            className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              currentView === 'design-system' 
                ? 'bg-sidebar-primary text-sidebar-primary-foreground' 
                : 'text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
            }`}
          >
            <Database className="w-5 h-5" />
            <span>Design System</span>
          </button>
          <button className="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors">
            <Server className="w-5 h-5" />
            <span>Devices</span>
          </button>
          <button className="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors">
            <HardDrive className="w-5 h-5" />
            <span>Storage</span>
          </button>
          <button className="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors">
            <RefreshCw className="w-5 h-5" />
            <span>Jobs</span>
          </button>
          <button className="w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors">
            <Settings className="w-5 h-5" />
            <span>Settings</span>
          </button>
        </nav>

        <button
          onClick={toggleDarkMode}
          className="mt-auto px-3 py-2 rounded-lg text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors"
        >
          {darkMode ? '☀️ Light Mode' : '🌙 Dark Mode'}
        </button>
      </aside>

      {/* Main Content */}
      <main className="flex-1 overflow-auto">
        <div className="p-8 max-w-7xl mx-auto">
          {renderContent()}
        </div>
      </main>
    </div>
  );
}

// Design System View Component
function DesignSystemView({ backupData }: { backupData: any[] }) {
  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1>BackupChrono Design System</h1>
        <p className="text-muted-foreground mt-2">
          A professional IT backup monitoring tool with clarity over beauty, built for 8+ hour monitoring sessions.
        </p>
      </div>

      {/* Status Overview Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Total Devices</p>
              <p className="text-2xl font-medium">42</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Server className="w-6 h-6 text-primary" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Successful</p>
              <p className="text-2xl font-medium">38</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-success/10 flex items-center justify-center">
              <span className="text-success">✓</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Warnings</p>
              <p className="text-2xl font-medium">3</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-warning/10 flex items-center justify-center">
              <span className="text-warning">⚠</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Failed</p>
              <p className="text-2xl font-medium">1</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-error/10 flex items-center justify-center">
              <span className="text-error">✕</span>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Typography Section */}
      <Card>
        <CardHeader>
          <CardTitle>Typography Scale</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <h1>Heading 1 - Page Title</h1>
            <p className="text-muted-foreground">1.5rem (24px) • Medium weight</p>
          </div>
          <div>
            <h2>Heading 2 - Section Title</h2>
            <p className="text-muted-foreground">1.25rem (20px) • Medium weight</p>
          </div>
          <div>
            <h3>Heading 3 - Card Title</h3>
            <p className="text-muted-foreground">1.125rem (18px) • Medium weight</p>
          </div>
          <div>
            <p>Body Text - Regular size for general content</p>
            <p className="text-muted-foreground">1rem (16px) • Normal weight</p>
          </div>
          <div>
            <code className="font-[var(--font-mono)] bg-muted px-2 py-1 rounded">/var/www/html/backup/2026-01-04</code>
            <p className="text-muted-foreground mt-1">Monospace font for paths, IPs, timestamps</p>
          </div>
        </CardContent>
      </Card>

      {/* Status Badges */}
      <Card>
        <CardHeader>
          <CardTitle>Status Badges</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-3">
          <StatusBadge status="success" label="Backup Complete" />
          <StatusBadge status="warning" label="Delayed" />
          <StatusBadge status="error" label="Failed" />
          <StatusBadge status="neutral" label="Pending" />
          <StatusBadge status="success" label="No Icon" showIcon={false} />
        </CardContent>
      </Card>

      {/* Buttons */}
      <Card>
        <CardHeader>
          <CardTitle>Buttons</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-3">
            <Button variant="primary">Primary Action</Button>
            <Button variant="secondary">Secondary Action</Button>
            <Button variant="danger">Delete Backup</Button>
            <Button variant="primary" disabled>Disabled</Button>
          </div>
          <div className="flex flex-wrap gap-3 items-center">
            <Button variant="primary" size="sm">Small</Button>
            <Button variant="primary" size="md">Medium</Button>
            <Button variant="primary" size="lg">Large</Button>
          </div>
        </CardContent>
      </Card>

      {/* Progress Bars */}
      <Card>
        <CardHeader>
          <CardTitle>Progress Bars - Storage Usage</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <ProgressBar value={45} label="Storage Pool A" variant="default" />
          <ProgressBar value={72} label="Storage Pool B" variant="success" />
          <ProgressBar value={89} label="Storage Pool C" variant="warning" />
          <ProgressBar value={96} label="Storage Pool D" variant="error" />
        </CardContent>
      </Card>

      {/* Forms */}
      <Card>
        <CardHeader>
          <CardTitle>Form Elements</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="Device Name" placeholder="prod-server-01" />
            <Input label="Backup Path" placeholder="/var/www/html" />
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input 
              label="Email Address" 
              type="email" 
              placeholder="admin@example.com" 
              variant="success"
              helperText="Email is valid"
            />
            <Input 
              label="Retention Days" 
              type="number" 
              placeholder="30" 
              variant="error"
              helperText="Must be between 1 and 365"
            />
          </div>
          <Select 
            label="Backup Schedule" 
            options={[
              { value: 'hourly', label: 'Hourly' },
              { value: 'daily', label: 'Daily' },
              { value: 'weekly', label: 'Weekly' },
              { value: 'monthly', label: 'Monthly' },
            ]}
          />
        </CardContent>
      </Card>

      {/* Data Table */}
      <Card>
        <CardHeader>
          <CardTitle>Backup Status Table</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow zebra={false}>
                <TableHead>Device</TableHead>
                <TableHead>Path</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Last Backup</TableHead>
                <TableHead>Size</TableHead>
                <TableHead>Files</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {backupData.map((backup) => (
                <TableRow key={backup.id}>
                  <TableCell>
                    <span className="font-medium">{backup.device}</span>
                  </TableCell>
                  <TableCell>
                    <code className="font-[var(--font-mono)] bg-muted px-1.5 py-0.5 rounded">
                      {backup.path}
                    </code>
                  </TableCell>
                  <TableCell>
                    <StatusBadge 
                      status={backup.status as 'success' | 'warning' | 'error'} 
                      label={backup.status.charAt(0).toUpperCase() + backup.status.slice(1)} 
                    />
                  </TableCell>
                  <TableCell>
                    <span className="font-[var(--font-mono)]">{backup.lastBackup}</span>
                  </TableCell>
                  <TableCell>{backup.size}</TableCell>
                  <TableCell>{backup.files}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Spacing System */}
      <Card>
        <CardHeader>
          <CardTitle>Spacing System (8px base)</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-4">
            <div className="w-1 h-8 bg-primary" title="4px"></div>
            <span className="text-muted-foreground">4px (spacing-1)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-2 h-8 bg-primary" title="8px"></div>
            <span className="text-muted-foreground">8px (spacing-2)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-4 h-8 bg-primary" title="16px"></div>
            <span className="text-muted-foreground">16px (spacing-3)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-6 h-8 bg-primary" title="24px"></div>
            <span className="text-muted-foreground">24px (spacing-4)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-8 h-8 bg-primary" title="32px"></div>
            <span className="text-muted-foreground">32px (spacing-5)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-12 h-8 bg-primary" title="48px"></div>
            <span className="text-muted-foreground">48px (spacing-6)</span>
          </div>
          <div className="flex items-center gap-4">
            <div className="w-16 h-8 bg-primary" title="64px"></div>
            <span className="text-muted-foreground">64px (spacing-7)</span>
          </div>
        </CardContent>
      </Card>

      {/* Color Palette */}
      <Card>
        <CardHeader>
          <CardTitle>Color Palette</CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div>
            <h4 className="mb-3">Status Colors</h4>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <div className="w-full h-16 bg-success rounded-lg mb-2"></div>
                <p>Success</p>
              </div>
              <div>
                <div className="w-full h-16 bg-warning rounded-lg mb-2"></div>
                <p>Warning</p>
              </div>
              <div>
                <div className="w-full h-16 bg-error rounded-lg mb-2"></div>
                <p>Error</p>
              </div>
              <div>
                <div className="w-full h-16 bg-neutral rounded-lg mb-2"></div>
                <p>Neutral</p>
              </div>
            </div>
          </div>
          <div>
            <h4 className="mb-3">Interactive Colors</h4>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              <div>
                <div className="w-full h-16 bg-primary rounded-lg mb-2"></div>
                <p>Primary</p>
              </div>
              <div>
                <div className="w-full h-16 bg-secondary rounded-lg mb-2 border border-border"></div>
                <p>Secondary</p>
              </div>
              <div>
                <div className="w-full h-16 bg-destructive rounded-lg mb-2"></div>
                <p>Destructive</p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}