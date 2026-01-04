import { useState, useEffect } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from './Card';
import { StatusBadge } from './StatusBadge';
import { ProgressBar } from './ProgressBar';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './Table';
import { LineChart, Line, AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { Server, Cpu, HardDrive, MemoryStick, Thermometer, Zap, Network, Clock } from 'lucide-react';

interface DeviceMetrics {
  id: number;
  name: string;
  type: 'server' | 'nas' | 'workstation';
  status: 'healthy' | 'warning' | 'critical' | 'offline';
  cpu: number;
  memory: number;
  disk: number;
  temperature: number;
  uptime: string;
  lastSeen: string;
  network: {
    inbound: string;
    outbound: string;
  };
}

export function DeviceHealth() {
  const [devices, setDevices] = useState<DeviceMetrics[]>([
    {
      id: 1,
      name: 'prod-server-01',
      type: 'server',
      status: 'healthy',
      cpu: 45,
      memory: 68,
      disk: 72,
      temperature: 58,
      uptime: '42d 8h 15m',
      lastSeen: 'Just now',
      network: { inbound: '125 MB/s', outbound: '89 MB/s' }
    },
    {
      id: 2,
      name: 'nas-storage',
      type: 'nas',
      status: 'warning',
      cpu: 23,
      memory: 54,
      disk: 89,
      temperature: 62,
      uptime: '127d 14h 32m',
      lastSeen: 'Just now',
      network: { inbound: '245 MB/s', outbound: '156 MB/s' }
    },
    {
      id: 3,
      name: 'db-server-02',
      type: 'server',
      status: 'critical',
      cpu: 92,
      memory: 94,
      disk: 78,
      temperature: 75,
      uptime: '15d 3h 45m',
      lastSeen: 'Just now',
      network: { inbound: '456 MB/s', outbound: '234 MB/s' }
    },
    {
      id: 4,
      name: 'workstation-05',
      type: 'workstation',
      status: 'healthy',
      cpu: 34,
      memory: 45,
      disk: 56,
      temperature: 52,
      uptime: '8d 12h 23m',
      lastSeen: 'Just now',
      network: { inbound: '45 MB/s', outbound: '23 MB/s' }
    },
    {
      id: 5,
      name: 'mail-server',
      type: 'server',
      status: 'healthy',
      cpu: 28,
      memory: 62,
      disk: 45,
      temperature: 54,
      uptime: '89d 5h 12m',
      lastSeen: 'Just now',
      network: { inbound: '78 MB/s', outbound: '92 MB/s' }
    }
  ]);

  const [selectedDevice, setSelectedDevice] = useState<DeviceMetrics>(devices[0]);
  const [historicalData, setHistoricalData] = useState<any[]>([]);

  // Generate historical data for the selected device
  useEffect(() => {
    const generateHistoricalData = () => {
      const data = [];
      const now = new Date();
      for (let i = 59; i >= 0; i--) {
        const time = new Date(now.getTime() - i * 60000);
        data.push({
          time: time.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
          cpu: Math.max(0, Math.min(100, selectedDevice.cpu + (Math.random() - 0.5) * 20)),
          memory: Math.max(0, Math.min(100, selectedDevice.memory + (Math.random() - 0.5) * 15)),
          disk: Math.max(0, Math.min(100, selectedDevice.disk + (Math.random() - 0.5) * 5)),
        });
      }
      return data;
    };

    setHistoricalData(generateHistoricalData());

    // Update historical data every 5 seconds
    const interval = setInterval(() => {
      setHistoricalData(prevData => {
        const newData = [...prevData.slice(1)];
        const lastPoint = newData[newData.length - 1];
        const now = new Date();
        newData.push({
          time: now.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }),
          cpu: Math.max(0, Math.min(100, lastPoint.cpu + (Math.random() - 0.5) * 10)),
          memory: Math.max(0, Math.min(100, lastPoint.memory + (Math.random() - 0.5) * 8)),
          disk: Math.max(0, Math.min(100, lastPoint.disk + (Math.random() - 0.5) * 3)),
        });
        return newData;
      });

      // Simulate slight metric changes
      setDevices(prevDevices =>
        prevDevices.map(device => ({
          ...device,
          cpu: Math.max(0, Math.min(100, device.cpu + (Math.random() - 0.5) * 5)),
          memory: Math.max(0, Math.min(100, device.memory + (Math.random() - 0.5) * 3)),
        }))
      );
    }, 5000);

    return () => clearInterval(interval);
  }, [selectedDevice]);

  const getStatusVariant = (status: DeviceMetrics['status']) => {
    switch (status) {
      case 'healthy': return 'success';
      case 'warning': return 'warning';
      case 'critical': return 'error';
      case 'offline': return 'neutral';
    }
  };

  const getMetricStatus = (value: number): 'default' | 'success' | 'warning' | 'error' => {
    if (value < 70) return 'success';
    if (value < 85) return 'warning';
    return 'error';
  };

  const healthyCount = devices.filter(d => d.status === 'healthy').length;
  const warningCount = devices.filter(d => d.status === 'warning').length;
  const criticalCount = devices.filter(d => d.status === 'critical').length;
  const offlineCount = devices.filter(d => d.status === 'offline').length;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1>Device Health Dashboard</h1>
        <p className="text-muted-foreground mt-1">
          Real-time monitoring of CPU, memory, disk, and system metrics across all devices
        </p>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Total Devices</p>
              <p className="text-2xl font-medium">{devices.length}</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Server className="w-6 h-6 text-primary" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Healthy</p>
              <p className="text-2xl font-medium">{healthyCount}</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-success/10 flex items-center justify-center">
              <span className="text-success text-2xl">✓</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Warning</p>
              <p className="text-2xl font-medium">{warningCount}</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-warning/10 flex items-center justify-center">
              <span className="text-warning text-2xl">⚠</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Critical</p>
              <p className="text-2xl font-medium">{criticalCount}</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-error/10 flex items-center justify-center">
              <span className="text-error text-2xl">✕</span>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Device List */}
      <Card>
        <CardHeader>
          <CardTitle>All Devices</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow zebra={false}>
                <TableHead>Device</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>CPU</TableHead>
                <TableHead>Memory</TableHead>
                <TableHead>Disk</TableHead>
                <TableHead>Temperature</TableHead>
                <TableHead>Uptime</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.map((device) => (
                <TableRow key={device.id}>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Server className="w-4 h-4 text-muted-foreground" />
                      <span className="font-medium">{device.name}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      status={getStatusVariant(device.status)}
                      label={device.status.charAt(0).toUpperCase() + device.status.slice(1)}
                    />
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 min-w-[120px]">
                      <div className="flex-1">
                        <ProgressBar
                          value={device.cpu}
                          variant={getMetricStatus(device.cpu)}
                          showPercentage={false}
                        />
                      </div>
                      <span className="font-[var(--font-mono)] w-12">{device.cpu}%</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 min-w-[120px]">
                      <div className="flex-1">
                        <ProgressBar
                          value={device.memory}
                          variant={getMetricStatus(device.memory)}
                          showPercentage={false}
                        />
                      </div>
                      <span className="font-[var(--font-mono)] w-12">{device.memory}%</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2 min-w-[120px]">
                      <div className="flex-1">
                        <ProgressBar
                          value={device.disk}
                          variant={getMetricStatus(device.disk)}
                          showPercentage={false}
                        />
                      </div>
                      <span className="font-[var(--font-mono)] w-12">{device.disk}%</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1">
                      <Thermometer className="w-4 h-4 text-muted-foreground" />
                      <span className="font-[var(--font-mono)]">{device.temperature}°C</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <span className="font-[var(--font-mono)]">{device.uptime}</span>
                  </TableCell>
                  <TableCell>
                    <button
                      onClick={() => setSelectedDevice(device)}
                      className="px-3 py-1 bg-primary text-primary-foreground rounded hover:bg-[var(--primary-hover)] transition-colors"
                    >
                      Details
                    </button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Device Details */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Device Details: {selectedDevice.name}</CardTitle>
            <StatusBadge
              status={getStatusVariant(selectedDevice.status)}
              label={selectedDevice.status.charAt(0).toUpperCase() + selectedDevice.status.slice(1)}
            />
          </div>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Current Metrics Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="p-4 border border-border rounded-lg">
              <div className="flex items-center gap-2 mb-3">
                <Cpu className="w-5 h-5 text-primary" />
                <span className="text-muted-foreground">CPU Usage</span>
              </div>
              <div className="text-2xl font-medium mb-2">{selectedDevice.cpu.toFixed(1)}%</div>
              <ProgressBar
                value={selectedDevice.cpu}
                variant={getMetricStatus(selectedDevice.cpu)}
                showPercentage={false}
              />
            </div>

            <div className="p-4 border border-border rounded-lg">
              <div className="flex items-center gap-2 mb-3">
                <MemoryStick className="w-5 h-5 text-warning" />
                <span className="text-muted-foreground">Memory</span>
              </div>
              <div className="text-2xl font-medium mb-2">{selectedDevice.memory.toFixed(1)}%</div>
              <ProgressBar
                value={selectedDevice.memory}
                variant={getMetricStatus(selectedDevice.memory)}
                showPercentage={false}
              />
            </div>

            <div className="p-4 border border-border rounded-lg">
              <div className="flex items-center gap-2 mb-3">
                <HardDrive className="w-5 h-5 text-success" />
                <span className="text-muted-foreground">Disk Usage</span>
              </div>
              <div className="text-2xl font-medium mb-2">{selectedDevice.disk.toFixed(1)}%</div>
              <ProgressBar
                value={selectedDevice.disk}
                variant={getMetricStatus(selectedDevice.disk)}
                showPercentage={false}
              />
            </div>

            <div className="p-4 border border-border rounded-lg">
              <div className="flex items-center gap-2 mb-3">
                <Thermometer className="w-5 h-5 text-error" />
                <span className="text-muted-foreground">Temperature</span>
              </div>
              <div className="text-2xl font-medium mb-2">{selectedDevice.temperature}°C</div>
              <ProgressBar
                value={(selectedDevice.temperature / 100) * 100}
                variant={selectedDevice.temperature > 70 ? 'error' : selectedDevice.temperature > 60 ? 'warning' : 'success'}
                showPercentage={false}
              />
            </div>
          </div>

          {/* Additional Info */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4 p-4 bg-muted/30 rounded-lg">
            <div className="flex items-center gap-3">
              <Clock className="w-5 h-5 text-muted-foreground" />
              <div>
                <div className="text-muted-foreground">Uptime</div>
                <div className="font-[var(--font-mono)]">{selectedDevice.uptime}</div>
              </div>
            </div>
            <div className="flex items-center gap-3">
              <Network className="w-5 h-5 text-muted-foreground" />
              <div>
                <div className="text-muted-foreground">Network In/Out</div>
                <div className="font-[var(--font-mono)]">
                  {selectedDevice.network.inbound} / {selectedDevice.network.outbound}
                </div>
              </div>
            </div>
            <div className="flex items-center gap-3">
              <Zap className="w-5 h-5 text-muted-foreground" />
              <div>
                <div className="text-muted-foreground">Last Seen</div>
                <div className="font-[var(--font-mono)]">{selectedDevice.lastSeen}</div>
              </div>
            </div>
          </div>

          {/* Historical Charts */}
          <div className="space-y-6">
            <div>
              <h4 className="mb-4">CPU & Memory Usage (Last Hour)</h4>
              <ResponsiveContainer width="100%" height={250}>
                <LineChart data={historicalData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" />
                  <XAxis
                    dataKey="time"
                    stroke="var(--color-muted-foreground)"
                    style={{ fontFamily: 'var(--font-mono)', fontSize: '12px' }}
                  />
                  <YAxis
                    stroke="var(--color-muted-foreground)"
                    style={{ fontFamily: 'var(--font-mono)' }}
                    label={{ value: 'Usage (%)', angle: -90, position: 'insideLeft' }}
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'var(--color-card)',
                      border: '1px solid var(--color-border)',
                      borderRadius: '0.5rem'
                    }}
                  />
                  <Legend />
                  <Line
                    type="monotone"
                    dataKey="cpu"
                    stroke="var(--color-primary)"
                    strokeWidth={2}
                    name="CPU"
                    dot={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="memory"
                    stroke="var(--color-warning)"
                    strokeWidth={2}
                    name="Memory"
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>

            <div>
              <h4 className="mb-4">Disk Usage Trend (Last Hour)</h4>
              <ResponsiveContainer width="100%" height={200}>
                <AreaChart data={historicalData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" />
                  <XAxis
                    dataKey="time"
                    stroke="var(--color-muted-foreground)"
                    style={{ fontFamily: 'var(--font-mono)', fontSize: '12px' }}
                  />
                  <YAxis
                    stroke="var(--color-muted-foreground)"
                    style={{ fontFamily: 'var(--font-mono)' }}
                    label={{ value: 'Usage (%)', angle: -90, position: 'insideLeft' }}
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'var(--color-card)',
                      border: '1px solid var(--color-border)',
                      borderRadius: '0.5rem'
                    }}
                  />
                  <Area
                    type="monotone"
                    dataKey="disk"
                    stroke="var(--color-success)"
                    fill="var(--color-success)"
                    fillOpacity={0.3}
                    name="Disk"
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
