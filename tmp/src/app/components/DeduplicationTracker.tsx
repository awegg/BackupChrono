import { Card, CardHeader, CardTitle, CardContent } from './Card';
import { ProgressBar } from './ProgressBar';
import { LineChart, Line, BarChart, Bar, PieChart, Pie, Cell, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { HardDrive, TrendingDown, Zap, Database } from 'lucide-react';

export function DeduplicationTracker() {
  // Mock data for deduplication savings over time
  const savingsOverTime = [
    { date: 'Jan 1', actual: 45.2, withoutDedup: 128.5 },
    { date: 'Jan 2', actual: 90.1, withoutDedup: 256.8 },
    { date: 'Jan 3', actual: 134.5, withoutDedup: 384.2 },
    { date: 'Jan 4', actual: 178.3, withoutDedup: 512.7 },
  ];

  // Mock data for daily deduplication rate
  const dailyDedup = [
    { date: 'Dec 29', rate: 64.2, saved: 52.3 },
    { date: 'Dec 30', rate: 65.8, saved: 58.1 },
    { date: 'Dec 31', rate: 63.5, saved: 49.7 },
    { date: 'Jan 1', rate: 64.9, saved: 83.3 },
    { date: 'Jan 2', rate: 66.2, saved: 86.7 },
    { date: 'Jan 3', rate: 65.1, saved: 79.8 },
    { date: 'Jan 4', rate: 64.8, saved: 84.4 },
  ];

  // Mock data for storage breakdown
  const storageBreakdown = [
    { name: 'Unique Data', value: 178.3, color: 'var(--color-primary)' },
    { name: 'Deduplicated', value: 334.4, color: 'var(--color-success)' },
  ];

  // Mock data for top deduplicated file types
  const fileTypeDedup = [
    { type: 'Log Files', saved: 89.2, percentage: 72 },
    { type: 'Database Backups', saved: 67.4, percentage: 68 },
    { type: 'VM Images', saved: 45.8, percentage: 58 },
    { type: 'Documents', saved: 38.1, percentage: 45 },
    { type: 'Media Files', saved: 12.3, percentage: 15 },
  ];

  const totalActual = 178.3;
  const totalWithoutDedup = 512.7;
  const totalSaved = totalWithoutDedup - totalActual;
  const savingsPercentage = ((totalSaved / totalWithoutDedup) * 100).toFixed(1);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1>Deduplication Savings Tracker</h1>
        <p className="text-muted-foreground mt-1">Track storage optimization and deduplication efficiency across all backups</p>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Total Saved</p>
              <p className="text-2xl font-medium font-[var(--font-mono)]">{totalSaved.toFixed(1)} GB</p>
              <p className="text-success mt-1">↓ {savingsPercentage}% reduction</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-success/10 flex items-center justify-center">
              <TrendingDown className="w-6 h-6 text-success" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Actual Storage</p>
              <p className="text-2xl font-medium font-[var(--font-mono)]">{totalActual} GB</p>
              <p className="text-muted-foreground mt-1">Physical usage</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <HardDrive className="w-6 h-6 text-primary" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Without Dedup</p>
              <p className="text-2xl font-medium font-[var(--font-mono)]">{totalWithoutDedup} GB</p>
              <p className="text-muted-foreground mt-1">Logical size</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-warning/10 flex items-center justify-center">
              <Database className="w-6 h-6 text-warning" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="flex items-center justify-between">
            <div>
              <p className="text-muted-foreground mb-1">Dedup Ratio</p>
              <p className="text-2xl font-medium">{(totalWithoutDedup / totalActual).toFixed(2)}:1</p>
              <p className="text-muted-foreground mt-1">Efficiency</p>
            </div>
            <div className="w-12 h-12 rounded-lg bg-accent/50 flex items-center justify-center">
              <Zap className="w-6 h-6 text-accent-foreground" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Storage Growth Chart */}
      <Card>
        <CardHeader>
          <CardTitle>Storage Growth - Actual vs. Without Deduplication</CardTitle>
        </CardHeader>
        <CardContent>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={savingsOverTime}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" />
              <XAxis 
                dataKey="date" 
                stroke="var(--color-muted-foreground)"
                style={{ fontFamily: 'var(--font-mono)' }}
              />
              <YAxis 
                stroke="var(--color-muted-foreground)"
                style={{ fontFamily: 'var(--font-mono)' }}
                label={{ value: 'Storage (GB)', angle: -90, position: 'insideLeft' }}
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
                dataKey="withoutDedup" 
                stroke="var(--color-warning)" 
                strokeWidth={2}
                name="Without Deduplication"
                dot={{ fill: 'var(--color-warning)' }}
              />
              <Line 
                type="monotone" 
                dataKey="actual" 
                stroke="var(--color-success)" 
                strokeWidth={2}
                name="Actual Storage"
                dot={{ fill: 'var(--color-success)' }}
              />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      {/* Daily Dedup Rate */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Daily Deduplication Rate</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={dailyDedup}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" />
                <XAxis 
                  dataKey="date" 
                  stroke="var(--color-muted-foreground)"
                  style={{ fontFamily: 'var(--font-mono)' }}
                />
                <YAxis 
                  stroke="var(--color-muted-foreground)"
                  style={{ fontFamily: 'var(--font-mono)' }}
                  label={{ value: 'Rate (%)', angle: -90, position: 'insideLeft' }}
                />
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'var(--color-card)', 
                    border: '1px solid var(--color-border)',
                    borderRadius: '0.5rem'
                  }}
                />
                <Bar dataKey="rate" fill="var(--color-primary)" name="Dedup Rate (%)" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Storage Distribution</CardTitle>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={storageBreakdown}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={({ name, value }) => `${name}: ${value} GB`}
                  outerRadius={100}
                  fill="#8884d8"
                  dataKey="value"
                >
                  {storageBreakdown.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip 
                  contentStyle={{ 
                    backgroundColor: 'var(--color-card)', 
                    border: '1px solid var(--color-border)',
                    borderRadius: '0.5rem'
                  }}
                />
              </PieChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      {/* Top Deduplicated File Types */}
      <Card>
        <CardHeader>
          <CardTitle>Deduplication by File Type</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {fileTypeDedup.map((item, index) => (
            <div key={index} className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="font-medium">{item.type}</span>
                <div className="flex items-center gap-4">
                  <span className="text-muted-foreground font-[var(--font-mono)]">{item.saved} GB saved</span>
                  <span className="text-success font-medium">{item.percentage}%</span>
                </div>
              </div>
              <ProgressBar 
                value={item.percentage} 
                variant="success"
                showPercentage={false}
              />
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Detailed Statistics */}
      <Card>
        <CardHeader>
          <CardTitle>Deduplication Statistics</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <div className="space-y-2">
              <p className="text-muted-foreground">Total Chunks Analyzed</p>
              <p className="text-xl font-[var(--font-mono)]">45,234,567</p>
            </div>
            <div className="space-y-2">
              <p className="text-muted-foreground">Unique Chunks</p>
              <p className="text-xl font-[var(--font-mono)]">15,782,341</p>
            </div>
            <div className="space-y-2">
              <p className="text-muted-foreground">Duplicate Chunks</p>
              <p className="text-xl font-[var(--font-mono)]">29,452,226</p>
            </div>
            <div className="space-y-2">
              <p className="text-muted-foreground">Average Chunk Size</p>
              <p className="text-xl font-[var(--font-mono)]">4.2 KB</p>
            </div>
            <div className="space-y-2">
              <p className="text-muted-foreground">Compression Ratio</p>
              <p className="text-xl font-[var(--font-mono)]">1.35:1</p>
            </div>
            <div className="space-y-2">
              <p className="text-muted-foreground">Combined Savings</p>
              <p className="text-xl font-[var(--font-mono)] text-success">73.4%</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
