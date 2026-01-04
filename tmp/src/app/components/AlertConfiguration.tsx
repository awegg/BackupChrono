import { useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from './Card';
import { Button } from './Button';
import { Input, Select } from './Input';
import { StatusBadge } from './StatusBadge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from './Table';
import { Bell, Mail, MessageSquare, Webhook, Plus, Trash2, Edit, TestTube } from 'lucide-react';

interface AlertRule {
  id: number;
  name: string;
  type: 'backup_failed' | 'backup_delayed' | 'storage_full' | 'device_offline';
  enabled: boolean;
  channels: string[];
  conditions: {
    severity?: string;
    duration?: string;
    threshold?: string;
  };
}

interface NotificationChannel {
  id: number;
  name: string;
  type: 'email' | 'slack' | 'webhook' | 'sms';
  destination: string;
  enabled: boolean;
}

export function AlertConfiguration() {
  const [alertRules, setAlertRules] = useState<AlertRule[]>([
    {
      id: 1,
      name: 'Critical Backup Failure',
      type: 'backup_failed',
      enabled: true,
      channels: ['email-admin', 'slack-ops'],
      conditions: { severity: 'critical' }
    },
    {
      id: 2,
      name: 'Backup Delayed > 2 Hours',
      type: 'backup_delayed',
      enabled: true,
      channels: ['email-admin'],
      conditions: { duration: '120' }
    },
    {
      id: 3,
      name: 'Storage Usage > 90%',
      type: 'storage_full',
      enabled: true,
      channels: ['email-admin', 'slack-ops'],
      conditions: { threshold: '90' }
    },
    {
      id: 4,
      name: 'Device Offline',
      type: 'device_offline',
      enabled: false,
      channels: ['slack-ops'],
      conditions: { duration: '15' }
    }
  ]);

  const [notificationChannels, setNotificationChannels] = useState<NotificationChannel[]>([
    {
      id: 1,
      name: 'email-admin',
      type: 'email',
      destination: 'admin@company.com',
      enabled: true
    },
    {
      id: 2,
      name: 'email-team',
      type: 'email',
      destination: 'backup-team@company.com',
      enabled: true
    },
    {
      id: 3,
      name: 'slack-ops',
      type: 'slack',
      destination: '#operations',
      enabled: true
    },
    {
      id: 4,
      name: 'webhook-monitoring',
      type: 'webhook',
      destination: 'https://monitoring.company.com/webhook',
      enabled: false
    }
  ]);

  const [showAddRule, setShowAddRule] = useState(false);
  const [showAddChannel, setShowAddChannel] = useState(false);

  const toggleRule = (id: number) => {
    setAlertRules(rules =>
      rules.map(rule =>
        rule.id === id ? { ...rule, enabled: !rule.enabled } : rule
      )
    );
  };

  const deleteRule = (id: number) => {
    setAlertRules(rules => rules.filter(rule => rule.id !== id));
  };

  const toggleChannel = (id: number) => {
    setNotificationChannels(channels =>
      channels.map(channel =>
        channel.id === id ? { ...channel, enabled: !channel.enabled } : channel
      )
    );
  };

  const deleteChannel = (id: number) => {
    setNotificationChannels(channels => channels.filter(channel => channel.id !== id));
  };

  const getAlertTypeLabel = (type: AlertRule['type']) => {
    const labels = {
      backup_failed: 'Backup Failed',
      backup_delayed: 'Backup Delayed',
      storage_full: 'Storage Full',
      device_offline: 'Device Offline'
    };
    return labels[type];
  };

  const getChannelIcon = (type: NotificationChannel['type']) => {
    switch (type) {
      case 'email': return <Mail className="w-4 h-4" />;
      case 'slack': return <MessageSquare className="w-4 h-4" />;
      case 'webhook': return <Webhook className="w-4 h-4" />;
      case 'sms': return <Bell className="w-4 h-4" />;
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1>Alert Configuration</h1>
        <p className="text-muted-foreground mt-1">
          Configure notifications for backup failures, delays, and system issues
        </p>
      </div>

      {/* Alert Rules */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Alert Rules</CardTitle>
            <Button variant="primary" size="sm" onClick={() => setShowAddRule(!showAddRule)}>
              <Plus className="w-4 h-4" />
              New Rule
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {showAddRule && (
            <div className="p-4 border border-border rounded-lg bg-muted/30 space-y-4">
              <h4>Create New Alert Rule</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Input label="Rule Name" placeholder="My Alert Rule" />
                <Select
                  label="Alert Type"
                  options={[
                    { value: 'backup_failed', label: 'Backup Failed' },
                    { value: 'backup_delayed', label: 'Backup Delayed' },
                    { value: 'storage_full', label: 'Storage Full' },
                    { value: 'device_offline', label: 'Device Offline' }
                  ]}
                />
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Input label="Condition Value" placeholder="e.g., 90 for 90%" type="number" />
                <Select
                  label="Notification Channels"
                  options={notificationChannels.filter(c => c.enabled).map(c => ({
                    value: c.name,
                    label: c.name
                  }))}
                />
              </div>
              <div className="flex gap-2">
                <Button variant="primary" size="sm">Save Rule</Button>
                <Button variant="secondary" size="sm" onClick={() => setShowAddRule(false)}>
                  Cancel
                </Button>
              </div>
            </div>
          )}

          <Table>
            <TableHeader>
              <TableRow zebra={false}>
                <TableHead>Status</TableHead>
                <TableHead>Rule Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Conditions</TableHead>
                <TableHead>Channels</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {alertRules.map((rule) => (
                <TableRow key={rule.id}>
                  <TableCell>
                    <button
                      onClick={() => toggleRule(rule.id)}
                      className={`w-12 h-6 rounded-full transition-colors ${
                        rule.enabled ? 'bg-success' : 'bg-neutral-bg'
                      }`}
                    >
                      <div
                        className={`w-5 h-5 rounded-full bg-white transition-transform ${
                          rule.enabled ? 'translate-x-6' : 'translate-x-0.5'
                        }`}
                      />
                    </button>
                  </TableCell>
                  <TableCell>
                    <span className="font-medium">{rule.name}</span>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      status={rule.enabled ? 'neutral' : 'neutral'}
                      label={getAlertTypeLabel(rule.type)}
                      showIcon={false}
                    />
                  </TableCell>
                  <TableCell>
                    <div className="font-[var(--font-mono)]">
                      {rule.conditions.severity && `Severity: ${rule.conditions.severity}`}
                      {rule.conditions.duration && `Duration: ${rule.conditions.duration}m`}
                      {rule.conditions.threshold && `Threshold: ${rule.conditions.threshold}%`}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex gap-1 flex-wrap">
                      {rule.channels.map((channel, idx) => (
                        <span
                          key={idx}
                          className="px-2 py-1 bg-muted rounded text-sm font-[var(--font-mono)]"
                        >
                          {channel}
                        </span>
                      ))}
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex gap-2">
                      <button className="p-1 hover:bg-muted rounded transition-colors">
                        <Edit className="w-4 h-4 text-muted-foreground" />
                      </button>
                      <button
                        onClick={() => deleteRule(rule.id)}
                        className="p-1 hover:bg-error-bg rounded transition-colors"
                      >
                        <Trash2 className="w-4 h-4 text-error" />
                      </button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Notification Channels */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Notification Channels</CardTitle>
            <Button variant="primary" size="sm" onClick={() => setShowAddChannel(!showAddChannel)}>
              <Plus className="w-4 h-4" />
              New Channel
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {showAddChannel && (
            <div className="p-4 border border-border rounded-lg bg-muted/30 space-y-4">
              <h4>Add Notification Channel</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Input label="Channel Name" placeholder="e.g., email-alerts" />
                <Select
                  label="Channel Type"
                  options={[
                    { value: 'email', label: 'Email' },
                    { value: 'slack', label: 'Slack' },
                    { value: 'webhook', label: 'Webhook' },
                    { value: 'sms', label: 'SMS' }
                  ]}
                />
              </div>
              <Input label="Destination" placeholder="e.g., alerts@company.com or #channel" />
              <div className="flex gap-2">
                <Button variant="primary" size="sm">
                  <TestTube className="w-4 h-4" />
                  Test & Save
                </Button>
                <Button variant="secondary" size="sm" onClick={() => setShowAddChannel(false)}>
                  Cancel
                </Button>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {notificationChannels.map((channel) => (
              <div
                key={channel.id}
                className="p-4 border border-border rounded-lg hover:border-primary/50 transition-colors"
              >
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-2">
                    {getChannelIcon(channel.type)}
                    <span className="font-medium">{channel.name}</span>
                  </div>
                  <button
                    onClick={() => toggleChannel(channel.id)}
                    className={`w-10 h-5 rounded-full transition-colors ${
                      channel.enabled ? 'bg-success' : 'bg-neutral-bg'
                    }`}
                  >
                    <div
                      className={`w-4 h-4 rounded-full bg-white transition-transform ${
                        channel.enabled ? 'translate-x-5' : 'translate-x-0.5'
                      }`}
                    />
                  </button>
                </div>
                <div className="space-y-2">
                  <div>
                    <div className="text-muted-foreground">Type</div>
                    <div className="capitalize">{channel.type}</div>
                  </div>
                  <div>
                    <div className="text-muted-foreground">Destination</div>
                    <div className="font-[var(--font-mono)] truncate">{channel.destination}</div>
                  </div>
                </div>
                <div className="flex gap-2 mt-4 pt-4 border-t border-border">
                  <Button variant="secondary" size="sm" className="flex-1">
                    <TestTube className="w-4 h-4" />
                    Test
                  </Button>
                  <Button variant="secondary" size="sm" className="flex-1">
                    <Edit className="w-4 h-4" />
                    Edit
                  </Button>
                  <button
                    onClick={() => deleteChannel(channel.id)}
                    className="px-3 py-1.5 hover:bg-error-bg rounded transition-colors"
                  >
                    <Trash2 className="w-4 h-4 text-error" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {/* Recent Alerts */}
      <Card>
        <CardHeader>
          <CardTitle>Recent Alerts (Last 24 Hours)</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow zebra={false}>
                <TableHead>Time</TableHead>
                <TableHead>Alert Rule</TableHead>
                <TableHead>Device</TableHead>
                <TableHead>Severity</TableHead>
                <TableHead>Message</TableHead>
                <TableHead>Channels</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>
                  <span className="font-[var(--font-mono)]">2026-01-04 15:30</span>
                </TableCell>
                <TableCell>
                  <span className="font-medium">Critical Backup Failure</span>
                </TableCell>
                <TableCell>db-server-02</TableCell>
                <TableCell>
                  <StatusBadge status="error" label="Critical" />
                </TableCell>
                <TableCell>Backup failed: Connection timeout after 30 minutes</TableCell>
                <TableCell>
                  <div className="flex gap-1">
                    <Mail className="w-4 h-4 text-muted-foreground" />
                    <MessageSquare className="w-4 h-4 text-muted-foreground" />
                  </div>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell>
                  <span className="font-[var(--font-mono)]">2026-01-04 11:45</span>
                </TableCell>
                <TableCell>
                  <span className="font-medium">Storage Usage &gt; 90%</span>
                </TableCell>
                <TableCell>nas-storage</TableCell>
                <TableCell>
                  <StatusBadge status="warning" label="Warning" />
                </TableCell>
                <TableCell>Storage pool at 92% capacity (2.1 TB / 2.3 TB)</TableCell>
                <TableCell>
                  <div className="flex gap-1">
                    <Mail className="w-4 h-4 text-muted-foreground" />
                    <MessageSquare className="w-4 h-4 text-muted-foreground" />
                  </div>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell>
                  <span className="font-[var(--font-mono)]">2026-01-04 09:20</span>
                </TableCell>
                <TableCell>
                  <span className="font-medium">Backup Delayed &gt; 2 Hours</span>
                </TableCell>
                <TableCell>workstation-05</TableCell>
                <TableCell>
                  <StatusBadge status="warning" label="Warning" />
                </TableCell>
                <TableCell>Backup scheduled for 07:00 started at 09:15 (2h 15m delay)</TableCell>
                <TableCell>
                  <div className="flex gap-1">
                    <Mail className="w-4 h-4 text-muted-foreground" />
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Alert Statistics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card>
          <CardContent className="flex flex-col">
            <div className="text-muted-foreground mb-2">Alerts Today</div>
            <div className="text-2xl font-medium">12</div>
            <div className="text-success mt-1">↓ 45% from yesterday</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex flex-col">
            <div className="text-muted-foreground mb-2">Critical Alerts</div>
            <div className="text-2xl font-medium">3</div>
            <div className="text-error mt-1">↑ 2 from yesterday</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex flex-col">
            <div className="text-muted-foreground mb-2">Active Rules</div>
            <div className="text-2xl font-medium">{alertRules.filter(r => r.enabled).length}</div>
            <div className="text-muted-foreground mt-1">of {alertRules.length} total</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex flex-col">
            <div className="text-muted-foreground mb-2">Channels Active</div>
            <div className="text-2xl font-medium">{notificationChannels.filter(c => c.enabled).length}</div>
            <div className="text-muted-foreground mt-1">of {notificationChannels.length} configured</div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}