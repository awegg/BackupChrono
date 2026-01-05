import React, { useEffect, useMemo, useState } from 'react';
import { deviceService } from '../services/deviceService';
import {
  Device,
  DeviceCreateDto,
  ProtocolType,
  RetentionPolicy,
  Schedule,
} from '../types';
import { ErrorDisplay } from './ErrorDisplay';

interface AddDeviceDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
  editingDeviceId?: string | null;
}

const protocolDefaults: Record<ProtocolType, number> = {
  [ProtocolType.SMB]: 445,
  [ProtocolType.SSH]: 22,
  [ProtocolType.Rsync]: 873,
};

const GLOBAL_DEFAULT_SCHEDULE = '0 2 * * *';
const GLOBAL_DEFAULT_RETENTION = {
  latest: 7,
  daily: 7,
  weekly: 4,
  monthly: 12,
  yearly: 3,
};

export function AddDeviceDialog({ open, onClose, onCreated, editingDeviceId }: AddDeviceDialogProps) {
  const [deviceName, setDeviceName] = useState('');
  const [showProtocolDropdown, setShowProtocolDropdown] = useState(false);
  const [protocol, setProtocol] = useState<ProtocolType>(ProtocolType.SMB);
  const [host, setHost] = useState('');
  const [port, setPort] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [wolEnabled, setWolEnabled] = useState(false);
  const [macAddress, setMacAddress] = useState('');
  const [schedule, setSchedule] = useState('');
  const [retentionLatest, setRetentionLatest] = useState('');
  const [retentionDaily, setRetentionDaily] = useState('');
  const [retentionWeekly, setRetentionWeekly] = useState('');
  const [retentionMonthly, setRetentionMonthly] = useState('');
  const [retentionYearly, setRetentionYearly] = useState('');
  const [includePatterns, setIncludePatterns] = useState('');
  const [excludePatterns, setExcludePatterns] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [testing, setTesting] = useState(false);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [showRetention, setShowRetention] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loadingDevice, setLoadingDevice] = useState(false);
  const isEditing = !!editingDeviceId;

  const resetForm = () => {
    setDeviceName('');
    setProtocol(ProtocolType.SMB);
    setHost('');
    setPort('');
    setUsername('');
    setPassword('');
    setWolEnabled(false);
    setMacAddress('');
    setSchedule('');
    setRetentionLatest('');
    setRetentionDaily('');
    setRetentionWeekly('');
    setRetentionMonthly('');
    setRetentionYearly('');
    setIncludePatterns('');
    setExcludePatterns('');
    setError(null);
    setShowAdvanced(false);
    setShowRetention(false);
    setTouched({});
  };

  useEffect(() => {
    if (open) {
      if (isEditing && editingDeviceId) {
        loadEditingDevice();
      } else {
        resetForm();
      }
    }
  }, [open, editingDeviceId, isEditing]);
  useEffect(() => {
    if (!port) {
      setPort(protocolDefaults[protocol].toString());
    }
  }, [protocol, port]);

  const loadEditingDevice = async () => {
    if (!editingDeviceId) return;
    setLoadingDevice(true);
    setError(null);
    try {
      const device = await deviceService.getDevice(editingDeviceId);
      populateFormFromDevice(device);
    } catch (err: any) {
      setError(err?.message || 'Failed to load device');
    } finally {
      setLoadingDevice(false);
    }
  };

  const populateFormFromDevice = (device: Device) => {
    setDeviceName(device.name);
    setProtocol(device.protocol);
    setHost(device.host);
    setPort(device.port?.toString() || '');
    setUsername(device.username);
    setPassword('');
    setWolEnabled(device.wakeOnLanEnabled);
    setMacAddress(device.wakeOnLanMacAddress || '');
    setSchedule(device.schedule?.cronExpression || '');
    setRetentionLatest(device.retentionPolicy?.keepLatest?.toString() || '');
    setRetentionDaily(device.retentionPolicy?.keepDaily?.toString() || '');
    setRetentionWeekly(device.retentionPolicy?.keepWeekly?.toString() || '');
    setRetentionMonthly(device.retentionPolicy?.keepMonthly?.toString() || '');
    setRetentionYearly(device.retentionPolicy?.keepYearly?.toString() || '');
    setIncludePatterns('');
    setExcludePatterns('');
    setShowAdvanced(!!device.schedule || !!device.retentionPolicy);
  };

  const validateDeviceName = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Device name is required';
    if (trimmed.length < 1 || trimmed.length > 100) return 'Must be 1-100 characters';
    if (!/^[a-zA-Z0-9.-]+$/.test(trimmed)) return 'Only alphanumeric characters, hyphens, and dots allowed';
    if (/^[.-]|[.-]$/.test(trimmed)) return 'Cannot start or end with dot or hyphen';
    if (/\.\./.test(trimmed)) return 'Cannot contain consecutive dots';
    return '';
  };

  const validateHost = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Host is required';
    if (trimmed.length < 1 || trimmed.length > 255) return 'Must be 1-255 characters';
    return '';
  };

  const validatePort = (value: string) => {
    if (!value) return '';
    const num = Number.parseInt(value, 10);
    if (Number.isNaN(num)) return 'Port must be a number';
    if (num < 1 || num > 65535) return 'Port must be between 1-65535';
    return '';
  };

  const validateMac = (value: string) => {
    if (wolEnabled && !value.trim()) return 'MAC address is required when Wake-on-LAN is enabled';
    if (value.trim() && !/^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$/.test(value.trim())) {
      return 'Invalid MAC address format (use XX:XX:XX:XX:XX:XX)';
    }
    return '';
  };

  const [touched, setTouched] = useState<Record<string, boolean>>({});

  const validationErrors = useMemo(() => {
    const errors: Record<string, string> = {};
    if (touched.deviceName) {
      const nameError = validateDeviceName(deviceName);
      if (nameError) errors.deviceName = nameError;
    }
    if (touched.host) {
      const hostError = validateHost(host);
      if (hostError) errors.host = hostError;
    }
    if (touched.port) {
      const portError = validatePort(port);
      if (portError) errors.port = portError;
    }
    if (touched.username && !username.trim()) errors.username = 'Username is required';
    if (touched.password && !password.trim() && !isEditing) errors.password = 'Password is required';
    if (touched.macAddress) {
      const macError = validateMac(macAddress);
      if (macError) errors.macAddress = macError;
    }
    return errors;
  }, [deviceName, host, port, username, password, macAddress, wolEnabled, isEditing, touched]);

  const isFormValid = deviceName.trim() && host.trim() && username.trim() && (password.trim() || isEditing) && (!wolEnabled || macAddress.trim()) && Object.keys(validationErrors).length === 0;

  const effectiveSchedule = schedule.trim() || `${GLOBAL_DEFAULT_SCHEDULE} (global)`;
  const effectiveScheduleSource = schedule.trim() ? 'Device' : 'Global';

  const effectiveRetention = (() => {
    const parts = {
      latest: retentionLatest.trim() || GLOBAL_DEFAULT_RETENTION.latest.toString(),
      daily: retentionDaily.trim() || GLOBAL_DEFAULT_RETENTION.daily.toString(),
      weekly: retentionWeekly.trim() || GLOBAL_DEFAULT_RETENTION.weekly.toString(),
      monthly: retentionMonthly.trim() || GLOBAL_DEFAULT_RETENTION.monthly.toString(),
      yearly: retentionYearly.trim() || GLOBAL_DEFAULT_RETENTION.yearly.toString(),
    };
    const source =
      retentionLatest || retentionDaily || retentionWeekly || retentionMonthly || retentionYearly
        ? 'Device'
        : 'Global';
    return {
      label: `${parts.latest}/${parts.daily}/${parts.weekly}/${parts.monthly}/${parts.yearly}`,
      source,
    };
  })();

  const effectivePatterns = (() => {
    const includeCount = includePatterns
      .split('\n')
      .map((p) => p.trim())
      .filter(Boolean).length;
    const excludeCount = excludePatterns
      .split('\n')
      .map((p) => p.trim())
      .filter(Boolean).length;
    const source = includeCount > 0 || excludeCount > 0 ? 'Device' : 'Global';
    return {
      includeCount,
      excludeCount,
      source,
    };
  })();

  const buildRetention = (): RetentionPolicy | undefined => {
    const r: RetentionPolicy = {};
    if (retentionLatest.trim()) r.keepLatest = Number(retentionLatest.trim());
    if (retentionDaily.trim()) r.keepDaily = Number(retentionDaily.trim());
    if (retentionWeekly.trim()) r.keepWeekly = Number(retentionWeekly.trim());
    if (retentionMonthly.trim()) r.keepMonthly = Number(retentionMonthly.trim());
    if (retentionYearly.trim()) r.keepYearly = Number(retentionYearly.trim());
    return Object.keys(r).length ? r : undefined;
  };

  const buildSchedule = (): Schedule | undefined => {
    const cron = schedule.trim();
    if (!cron) return undefined;
    return { cronExpression: cron };
  };

  const buildPatterns = (value: string) =>
    value
      .split('\n')
      .map((p) => p.trim())
      .filter(Boolean);

  const handleTestConnection = async () => {
    if (!isFormValid) {
      setError('Please fix validation errors before testing the connection.');
      return;
    }
    setError(null);
    setTesting(true);
    setTimeout(() => {
      setTesting(false);
    }, 1200);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid) {
      setError('Please fix validation errors before submitting.');
      return;
    }
    const finalPort = port.trim() ? Number.parseInt(port.trim(), 10) : protocolDefaults[protocol];

    const payload: any = {
      name: deviceName.trim(),
      protocol,
      host: host.trim(),
      port: finalPort,
      username: username.trim(),
      wakeOnLanEnabled: wolEnabled,
      wakeOnLanMacAddress: wolEnabled ? macAddress.trim() : undefined,
      schedule: buildSchedule(),
      retentionPolicy: buildRetention(),
    };

    if (password.trim()) {
      payload.password = password.trim();
    } else if (!isEditing) {
      payload.password = '';
    }

    const includes = buildPatterns(includePatterns);
    const excludes = buildPatterns(excludePatterns);
    if (includes.length || excludes.length) {
      payload.includeExcludeRules = {
        includePatterns: includes.length ? includes : undefined,
        excludePatterns: excludes.length ? excludes : undefined,
      };
    }

    setSubmitting(true);
    setError(null);
    try {
      if (isEditing && editingDeviceId) {
        await deviceService.updateDevice(editingDeviceId, payload);
      } else {
        if (!payload.password) {
          setError('Password is required for new devices');
          setSubmitting(false);
          return;
        }
        await deviceService.createDevice(payload as DeviceCreateDto);
      }
      onCreated?.();
      onClose();
    } catch (err: any) {
      setError(err?.message || 'Failed to save device');
    } finally {
      setSubmitting(false);
    }
  };

  if (!open) return null;

  if (loadingDevice) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
        <div className="bg-white rounded-lg p-6 shadow-xl">
          <div className="flex items-center gap-3">
            <div className="animate-spin rounded-full h-5 w-5 border border-blue-300 border-t-blue-600" />
            <span className="text-gray-700">Loading device...</span>
          </div>
        </div>
      </div>
    );
  }

  const dialogTitle = isEditing ? 'Edit Device' : 'Add New Device';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-2xl bg-white rounded-lg shadow-xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="border-b border-gray-200 px-6 py-4 flex items-start justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">{dialogTitle}</h2>
            <p className="text-sm text-gray-600 mt-1">
              {isEditing ? 'Update the backup device configuration.' : 'Configure a new backup device and its connection settings.'}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
            aria-label="Close"
          >
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {error && <div className="mb-4"><ErrorDisplay error={error} /></div>}

          <form id="add-device-form" onSubmit={handleSubmit} className="space-y-4">
            {/* Device Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Device Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={deviceName}
                onChange={(e) => setDeviceName(e.target.value)}
                onBlur={() => setTouched(prev => ({ ...prev, deviceName: true }))}
                placeholder="e.g., office-nas, web-server-1"
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
              {validationErrors.deviceName && (
                <p className="text-xs text-red-600 mt-1">{validationErrors.deviceName}</p>
              )}
            </div>

            {/* Protocol */}
            <div className="relative">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Protocol <span className="text-red-500">*</span>
              </label>
              <button
                type="button"
                onClick={() => setShowProtocolDropdown(!showProtocolDropdown)}
                onBlur={() => setTimeout(() => setShowProtocolDropdown(false), 200)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 bg-white hover:bg-gray-50 focus:ring-2 focus:ring-blue-500 focus:border-transparent text-left flex items-center justify-between"
              >
                <span className="flex items-center gap-2">
                  {protocol === ProtocolType.SMB && (
                    <>
                      <svg className="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <rect x="3" y="4" width="18" height="12" rx="1" strokeWidth="2"/>
                        <path d="M7 20h10M12 16v4" strokeWidth="2" strokeLinecap="round"/>
                      </svg>
                      <span>SMB/CIFS</span>
                    </>
                  )}
                  {protocol === ProtocolType.SSH && (
                    <>
                      <svg className="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" strokeWidth="2"/>
                        <polyline points="14 2 14 8 20 8" strokeWidth="2"/>
                      </svg>
                      <span>SSH/SFTP</span>
                    </>
                  )}
                  {protocol === ProtocolType.Rsync && (
                    <>
                      <svg className="w-4 h-4 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                      </svg>
                      <span>Rsync</span>
                    </>
                  )}
                </span>
                <svg className="w-4 h-4 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M6 9l6 6 6-6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </button>
              {showProtocolDropdown && (
                <div className="absolute z-10 w-full mt-1 bg-white border border-gray-300 rounded-md shadow-lg">
                  <button
                    type="button"
                    onClick={() => { setProtocol(ProtocolType.SMB); setShowProtocolDropdown(false); }}
                    className="w-full px-3 py-2 text-left hover:bg-blue-600 hover:text-white flex items-center gap-2 text-sm text-gray-900 hover:text-white"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <rect x="3" y="4" width="18" height="12" rx="1" strokeWidth="2"/>
                      <path d="M7 20h10M12 16v4" strokeWidth="2" strokeLinecap="round"/>
                    </svg>
                    <span>SMB/CIFS</span>
                    {protocol === ProtocolType.SMB && (
                      <svg className="w-4 h-4 ml-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <polyline points="20 6 9 17 4 12" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => { setProtocol(ProtocolType.SSH); setShowProtocolDropdown(false); }}
                    className="w-full px-3 py-2 text-left hover:bg-blue-600 hover:text-white flex items-center gap-2 text-sm text-gray-900 hover:text-white"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" strokeWidth="2"/>
                      <polyline points="14 2 14 8 20 8" strokeWidth="2"/>
                    </svg>
                    <span>SSH/SFTP</span>
                    {protocol === ProtocolType.SSH && (
                      <svg className="w-4 h-4 ml-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <polyline points="20 6 9 17 4 12" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                  </button>
                  <button
                    type="button"
                    onClick={() => { setProtocol(ProtocolType.Rsync); setShowProtocolDropdown(false); }}
                    className="w-full px-3 py-2 text-left hover:bg-blue-600 hover:text-white flex items-center gap-2 text-sm text-gray-900 hover:text-white"
                  >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                    <span>Rsync</span>
                    {protocol === ProtocolType.Rsync && (
                      <svg className="w-4 h-4 ml-auto" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <polyline points="20 6 9 17 4 12" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                  </button>
                </div>
              )}
            </div>

            {/* Host and Port */}
            <div className="grid grid-cols-3 gap-4">
              <div className="col-span-2">
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Host <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={host}
                  onChange={(e) => setHost(e.target.value)}
                  onBlur={() => setTouched(prev => ({ ...prev, host: true }))}
                  placeholder="192.168.1.100 or nas.local"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                {validationErrors.host && (
                  <p className="text-xs text-red-600 mt-1">{validationErrors.host}</p>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Port</label>
                <input
                  type="number"
                  value={port}
                  onChange={(e) => setPort(e.target.value)}
                  onBlur={() => setTouched(prev => ({ ...prev, port: true }))}
                  placeholder={protocolDefaults[protocol].toString()}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                {validationErrors.port && (
                  <p className="text-xs text-red-600 mt-1">{validationErrors.port}</p>
                )}
              </div>
            </div>

            {/* Username and Password */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Username <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  onBlur={() => setTouched(prev => ({ ...prev, username: true }))}
                  placeholder="backup-user"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                {validationErrors.username && (
                  <p className="text-xs text-red-600 mt-1">{validationErrors.username}</p>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Password <span className="text-red-500">*</span>
                </label>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onBlur={() => setTouched(prev => ({ ...prev, password: true }))}
                  placeholder="••••••••"
                  className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                {validationErrors.password && (
                  <p className="text-xs text-red-600 mt-1">{validationErrors.password}</p>
                )}
                {!validationErrors.password && (
                  <p className="text-xs text-gray-500 mt-1">
                    {isEditing ? 'Leave empty to keep current password' : 'Stored encrypted'}
                  </p>
                )}
              </div>
            </div>

            {/* Wake-on-LAN */}
            <div className="border-t pt-4">
              <div className="flex items-center gap-3">
                <input
                  type="checkbox"
                  id="wol"
                  checked={wolEnabled}
                  onChange={(e) => setWolEnabled(e.target.checked)}
                  className="w-4 h-4 rounded border-gray-300"
                />
                <label htmlFor="wol" className="text-sm font-medium text-gray-700">
                  Wake-on-LAN
                </label>
              </div>

              {wolEnabled && (
                <div className="mt-3 ml-7">
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    MAC Address <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={macAddress}
                    onChange={(e) => setMacAddress(e.target.value)}
                    onBlur={() => setTouched(prev => ({ ...prev, macAddress: true }))}
                    placeholder="AA:BB:CC:DD:EE:FF"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                  {validationErrors.macAddress && (
                    <p className="text-xs text-red-600 mt-1">{validationErrors.macAddress}</p>
                  )}
                </div>
              )}
            </div>

            {/* Advanced Settings */}
            <button
              type="button"
              onClick={() => setShowAdvanced(!showAdvanced)}
              className="w-full flex items-center gap-2 px-3 py-2 border border-gray-300 rounded-md hover:bg-gray-50 text-left mt-4"
            >
              {showAdvanced ? (
                <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M19 9l-7 7-7-7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              ) : (
                <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M9 5l7 7-7 7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              )}
              <span className="font-medium text-gray-700">Advanced Settings</span>
            </button>

            {showAdvanced && (
              <div className="bg-gray-50 rounded-md p-4 space-y-4 border border-gray-200">
                {/* Schedule */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Device-Level Schedule</label>
                  <input
                    type="text"
                    value={schedule}
                    onChange={(e) => setSchedule(e.target.value)}
                    placeholder={GLOBAL_DEFAULT_SCHEDULE}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                  <p className="text-xs text-gray-500 mt-1">Leave empty to use global default (2 AM daily)</p>
                </div>

                {/* Retention */}
                <div>
                  <button
                    type="button"
                    onClick={() => setShowRetention(!showRetention)}
                    className="flex items-center gap-2 text-sm font-medium text-gray-700 w-full"
                  >
                    {showRetention ? (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M19 9l-7 7-7-7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    ) : (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M9 5l7 7-7 7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                    <span>Retention Policy</span>
                  </button>
                  {showRetention && (
                    <div className="mt-3 grid grid-cols-2 md:grid-cols-3 gap-2">
                      <div>
                        <label className="block text-xs font-medium text-gray-600 mb-1">Latest</label>
                        <input
                          type="number"
                          value={retentionLatest}
                          onChange={(e) => setRetentionLatest(e.target.value)}
                          placeholder={GLOBAL_DEFAULT_RETENTION.latest.toString()}
                          className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-600 mb-1">Daily</label>
                        <input
                          type="number"
                          value={retentionDaily}
                          onChange={(e) => setRetentionDaily(e.target.value)}
                          placeholder={GLOBAL_DEFAULT_RETENTION.daily.toString()}
                          className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-600 mb-1">Weekly</label>
                        <input
                          type="number"
                          value={retentionWeekly}
                          onChange={(e) => setRetentionWeekly(e.target.value)}
                          placeholder={GLOBAL_DEFAULT_RETENTION.weekly.toString()}
                          className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-600 mb-1">Monthly</label>
                        <input
                          type="number"
                          value={retentionMonthly}
                          onChange={(e) => setRetentionMonthly(e.target.value)}
                          placeholder={GLOBAL_DEFAULT_RETENTION.monthly.toString()}
                          className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-600 mb-1">Yearly</label>
                        <input
                          type="number"
                          value={retentionYearly}
                          onChange={(e) => setRetentionYearly(e.target.value)}
                          placeholder={GLOBAL_DEFAULT_RETENTION.yearly.toString()}
                          className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                        />
                      </div>
                    </div>
                  )}
                  <p className="text-xs text-gray-500 mt-1">Leave empty to use global defaults</p>
                </div>

                {/* Patterns */}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Include Patterns</label>
                    <textarea
                      value={includePatterns}
                      onChange={(e) => setIncludePatterns(e.target.value)}
                      placeholder="*.pdf&#10;*.docx&#10;Documents/**"
                      className="w-full px-2 py-1 border border-gray-300 rounded text-xs text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent h-20"
                    />
                    <p className="text-xs text-gray-500 mt-1">One per line</p>
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Exclude Patterns</label>
                    <textarea
                      value={excludePatterns}
                      onChange={(e) => setExcludePatterns(e.target.value)}
                      placeholder="*.tmp&#10;.git/**&#10;node_modules/**"
                      className="w-full px-2 py-1 border border-gray-300 rounded text-xs text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent h-20"
                    />
                    <p className="text-xs text-gray-500 mt-1">One per line</p>
                  </div>
                </div>
              </div>
            )}

            {/* Configuration Hierarchy */}
            <div className="bg-blue-50 border border-blue-200 rounded-md p-3 mt-4">
              <div className="flex items-center gap-2 text-sm">
                <span className="inline-block px-2 py-0.5 bg-white border border-gray-300 rounded text-xs font-medium text-gray-700">Global Defaults</span>
                <span className="text-gray-500">→</span>
                <span className="inline-block px-2 py-0.5 bg-blue-100 border border-blue-300 rounded text-xs font-medium text-blue-700">Device Settings (you are here)</span>
              </div>
            </div>

            {/* Effective Configuration Preview */}
            <div className="bg-gray-50 border border-gray-200 rounded-md p-4">
              <h3 className="font-semibold text-sm text-gray-900 mb-3">Effective Configuration</h3>
              <div className="space-y-3">
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-medium text-gray-600">Schedule</span>
                    <span className={`text-xs font-medium px-2 py-0.5 rounded ${
                      effectiveScheduleSource === 'Device'
                        ? 'bg-blue-100 text-blue-700'
                        : 'bg-gray-200 text-gray-700'
                    }`}>
                      {effectiveScheduleSource}
                    </span>
                  </div>
                  <div className="font-mono text-xs text-gray-900">{effectiveSchedule}</div>
                </div>
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-medium text-gray-600">Retention</span>
                    <span className={`text-xs font-medium px-2 py-0.5 rounded ${
                      effectiveRetention.source === 'Device'
                        ? 'bg-blue-100 text-blue-700'
                        : 'bg-gray-200 text-gray-700'
                    }`}>
                      {effectiveRetention.source}
                    </span>
                  </div>
                  <div className="font-mono text-xs text-gray-900">{effectiveRetention.label}</div>
                  <p className="text-xs text-gray-500 mt-0.5">Latest/Daily/Weekly/Monthly/Yearly</p>
                </div>
                <div>
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-medium text-gray-600">Patterns</span>
                    <span className={`text-xs font-medium px-2 py-0.5 rounded ${
                      effectivePatterns.source === 'Device'
                        ? 'bg-blue-100 text-blue-700'
                        : 'bg-gray-200 text-gray-700'
                    }`}>
                      {effectivePatterns.source}
                    </span>
                  </div>
                  <div className="text-xs text-gray-900">
                    {effectivePatterns.includeCount || effectivePatterns.excludeCount
                      ? `${effectivePatterns.includeCount} includes, ${effectivePatterns.excludeCount} excludes`
                      : 'Using global patterns'}
                  </div>
                </div>
              </div>
            </div>
          </form>
        </div>

        {/* Footer */}
        <div className="border-t border-gray-200 px-6 py-3 flex items-center justify-between bg-gray-50">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-100"
          >
            Cancel
          </button>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={handleTestConnection}
              disabled={testing}
              className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-100 disabled:opacity-50"
            >
              {testing ? 'Testing...' : 'Test Connection'}
            </button>
            <button
              type="submit"
              form="add-device-form"
              disabled={!isFormValid || submitting || testing}
              className="px-4 py-2 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 disabled:opacity-50"
            >
              {submitting ? (isEditing ? 'Updating...' : 'Adding...') : (isEditing ? 'Update Device' : 'Add Device')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
