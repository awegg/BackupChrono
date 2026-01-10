import React, { useState } from 'react';
import cronstrue from 'cronstrue';
import { ShareCreateDto, Schedule, RetentionPolicy } from '../types';
import { shareService } from '../services/deviceService';

interface DeviceLike {
  id: string;
  name: string;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
}

interface ShareLike {
  id: string;
  name: string;
  path: string;
  enabled: boolean;
  schedule?: Schedule;
  retentionPolicy?: RetentionPolicy;
  includeExcludeRules?: {
    excludePatterns?: string[];
    excludeRegex?: string[];
    includeOnlyRegex?: string[];
    excludeIfPresent?: string[];
  };
}

interface AddShareDialogProps {
  open: boolean;
  onClose: () => void;
  device: DeviceLike | null;
  onCreated?: () => void;
  editingShare?: ShareLike | null;
}

// Mock device-level inherited config
const getDeviceConfig = (device: DeviceLike) => ({
  schedule: device.schedule || '0 2 * * *',
  scheduleDesc: device.schedule ? 'Device schedule' : '2 AM daily (global)',
  retention: {
    latest: device.retentionPolicy?.keepLatest ?? 7,
    daily: device.retentionPolicy?.keepDaily ?? 7,
    weekly: device.retentionPolicy?.keepWeekly ?? 4,
    monthly: 12,
    yearly: 3,
  },
});

export function AddShareDialog({ open, onClose, device, onCreated, editingShare }: AddShareDialogProps) {
  // Basic fields (initialize from editingShare if present)
  const [shareName, setShareName] = useState(() => editingShare?.name ?? '');
  const [sharePath, setSharePath] = useState(() => editingShare?.path ?? '');
  const [description, setDescription] = useState('');

  // Configuration overrides
  const [schedule, setSchedule] = useState(() => editingShare?.schedule?.cronExpression ?? '');
  const [retentionLatest, setRetentionLatest] = useState(() => editingShare?.retentionPolicy?.keepLatest?.toString() ?? '');
  const [retentionDaily, setRetentionDaily] = useState(() => editingShare?.retentionPolicy?.keepDaily?.toString() ?? '');
  const [retentionWeekly, setRetentionWeekly] = useState(() => editingShare?.retentionPolicy?.keepWeekly?.toString() ?? '');
  const [retentionMonthly, setRetentionMonthly] = useState(() => editingShare?.retentionPolicy?.keepMonthly?.toString() ?? '');
  const [retentionYearly, setRetentionYearly] = useState(() => editingShare?.retentionPolicy?.keepYearly?.toString() ?? '');
  const [includePatterns, setIncludePatterns] = useState(() => '');
  const [excludePatterns, setExcludePatterns] = useState(() => editingShare?.includeExcludeRules?.excludePatterns?.join('\n') ?? '');

  // Collapsible sections
  const [showSchedule, setShowSchedule] = useState(() => !!editingShare?.schedule);
  const [showRetention, setShowRetention] = useState(() => !!editingShare?.retentionPolicy);
  const [showPatterns, setShowPatterns] = useState(() => !!editingShare?.includeExcludeRules);

  // Validation
  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [error, setError] = useState<string | null>(null);

  const isEditing = !!editingShare;

  const resetForm = () => {
    setShareName('');
    setSharePath('');
    setDescription('');
    setSchedule('');
    setRetentionLatest('');
    setRetentionDaily('');
    setRetentionWeekly('');
    setRetentionMonthly('');
    setRetentionYearly('');
    setIncludePatterns('');
    setExcludePatterns('');
    setShowSchedule(false);
    setShowRetention(false);
    setShowPatterns(false);
    setTouched({});
    setError(null);
  };

  // Note: When editingShare changes, we re-mount the dialog to reinitialize state

  if (!device) return null;

  const deviceConfig = getDeviceConfig(device);

  const validateShareName = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Share name is required';
    if (trimmed.length < 1 || trimmed.length > 100) return 'Must be 1-100 characters';
    return '';
  };

  const validateSharePath = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return 'Share path is required';
    if (trimmed.length < 1 || trimmed.length > 500) return 'Must be 1-500 characters';
    return '';
  };

  const validateCronExpression = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return '';

    const parts = trimmed.split(/\s+/);
    if (parts.length < 6 || parts.length > 7) return 'Use 6 or 7-part Quartz cron (sec min hour day month day-of-week [year])';

    const secondsMinutes = /^([0-5]?\d|\*|\*\/[1-9]\d*|[0-5]?\d\/[1-9]\d*|[0-5]?\d-[0-5]?\d|([0-5]?\d,)+[0-5]?\d)$/;
    const hours = /^([01]?\d|2[0-3]|\*|\*\/[1-9]\d*|([01]?\d|2[0-3])\/[1-9]\d*|([01]?\d|2[0-3])-([01]?\d|2[0-3])|(([01]?\d|2[0-3]),)+([01]?\d|2[0-3]))$/;
    const dayOfMonth = /^([1-9]|[12]\d|3[01]|\*|\?|L|LW|L-[1-9]|[1-9]W|[12]\dW|3[01]W|([1-9]|[12]\d|3[01])-([1-9]|[12]\d|3[01])|([1-9]|[12]\d|3[01])\/[1-9]\d*|(([1-9]|[12]\d|3[01]),)+([1-9]|[12]\d|3[01]))$/;
    const month = /^(1[0-2]|0?[1-9]|JAN|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC|\*|\*\/[1-9]\d*|(1[0-2]|0?[1-9])-(1[0-2]|0?[1-9])|(1[0-2]|0?[1-9])\/[1-9]\d*|((1[0-2]|0?[1-9]),)+(1[0-2]|0?[1-9]))$/i;
    const dayOfWeek = /^(SUN|MON|TUE|WED|THU|FRI|SAT|[0-6]|\?|\*|\*\/[1-9]\d*|[0-6]\/[1-9]\d*|[0-6]-[0-6]|(SUN|MON|TUE|WED|THU|FRI|SAT)-[0-6]|(SUN|MON|TUE|WED|THU|FRI|SAT)(,(SUN|MON|TUE|WED|THU|FRI|SAT|[0-6]))+|[0-6](,[0-6])+|[0-6]#[1-5]|L|LW)$/i;
    const year = /^([12]\d{3}|\*|\*\/[1-9]\d*|[12]\d{3}\/[1-9]\d*|[12]\d{3}-[12]\d{3}|([12]\d{3},)+[12]\d{3})$/;

    const [sec, min, hr, dom, mon, dow, yr] = parts;

    if (!secondsMinutes.test(sec)) return 'Second field is invalid';
    if (!secondsMinutes.test(min)) return 'Minute field is invalid';
    if (!hours.test(hr)) return 'Hour field is invalid';
    if (!dayOfMonth.test(dom)) return 'Day-of-month field is invalid';
    if (!month.test(mon)) return 'Month field is invalid';
    if (!dayOfWeek.test(dow)) return 'Day-of-week field is invalid';

    if (yr && !year.test(yr)) return 'Year field is invalid';

    // Quartz requires either day-of-month or day-of-week to be '?' (not both can be * or both specific)
    const domSpecific = dom !== '*' && dom !== '?';
    const dowSpecific = dow !== '*' && dow !== '?';
    if (domSpecific && dowSpecific) return "Use '?' in day-of-month or day-of-week (only one can be specific)";
    if (dom === '*' && dow === '*') return "Use '?' for day-of-week (cannot have both day-of-month and day-of-week as '*')";

    return '';
  };

  const describeCron = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return '';
    
    try {
      // cronstrue expects 5-part standard cron; Quartz uses 6 parts (includes seconds)
      // Convert Quartz (sec min hr dom mon dow [year]) to standard (min hr dom mon dow)
      const parts = trimmed.split(/\s+/);
      if (parts.length < 6) return '';
      
      // Skip seconds (first field), take min hr dom mon dow
      // Normalize Quartz '?' to '*' for cronstrue compatibility
      const standardCron = parts.slice(1, 6).map(p => p === '?' ? '*' : p).join(' ');
      return cronstrue.toString(standardCron);
    } catch {
      return '';
    }
  };

  // Compute validation errors for display (touched-gated)
  const validationErrors = {
    shareName: touched.shareName ? validateShareName(shareName) : '',
    sharePath: touched.sharePath ? validateSharePath(sharePath) : '',
    schedule: touched.schedule ? validateCronExpression(schedule) : '',
  };

  // Compute raw validation for form validity (not touched-gated)
  const rawErrors = {
    shareName: validateShareName(shareName),
    sharePath: validateSharePath(sharePath),
    schedule: validateCronExpression(schedule),
  };

  const isFormValid = Boolean(
    shareName.trim() && sharePath.trim() &&
    !rawErrors.shareName && !rawErrors.sharePath && !rawErrors.schedule
  );

  const cronDescription = schedule && !validationErrors.schedule ? describeCron(schedule) : '';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!isFormValid) {
      setTouched(prev => ({ ...prev, shareName: true, sharePath: true, schedule: !!schedule }));
      return;
    }

    const payload: ShareCreateDto = {
      name: shareName.trim(),
      path: sharePath.trim(),
      enabled: true,
    };

    if (schedule.trim()) {
      payload.schedule = { cronExpression: schedule.trim() };
    }

    if (retentionLatest || retentionDaily || retentionWeekly || retentionMonthly || retentionYearly) {
      payload.retentionPolicy = {
        keepLatest: retentionLatest ? Number.parseInt(retentionLatest) : undefined,
        keepDaily: retentionDaily ? Number.parseInt(retentionDaily) : undefined,
        keepWeekly: retentionWeekly ? Number.parseInt(retentionWeekly) : undefined,
        keepMonthly: retentionMonthly ? Number.parseInt(retentionMonthly) : undefined,
        keepYearly: retentionYearly ? Number.parseInt(retentionYearly) : undefined,
      };
    }

    const includes = includePatterns ? includePatterns.split('\n').filter(p => p.trim()) : [];
    const excludes = excludePatterns ? excludePatterns.split('\n').filter(p => p.trim()) : [];
    if (includes.length || excludes.length) {
      payload.includeExcludeRules = {
        // Backend schema: excludePatterns, excludeRegex, includeOnlyRegex, excludeIfPresent
        // For now, only sending excludePatterns. includePatterns UI field is reserved for future use.
        excludePatterns: excludes.length ? excludes : undefined,
      };
    }

    setError(null);
    try {
      if (isEditing && editingShare) {
        await shareService.updateShare(device.id, editingShare.id, payload);
      } else {
        await shareService.createShare(device.id, payload);
      }
      resetForm();
      onCreated?.();
      onClose();
    } catch (err) {
      let errorMsg = isEditing ? 'Failed to update share' : 'Failed to create share';
      if (err && typeof err === 'object') {
        const maybeResponse = (err as { response?: { data?: { detail?: string; error?: string } } }).response;
        errorMsg = maybeResponse?.data?.detail
          || maybeResponse?.data?.error
          || (err as { message?: string }).message
          || errorMsg;
      }
      setError(errorMsg);
    }
  };

  // Effective configuration calculations
  const effectiveSchedule = schedule || deviceConfig.scheduleDesc;
  const effectiveScheduleSource = schedule ? 'Share' : 'Device';

  const hasRetentionOverride = retentionLatest || retentionDaily || retentionWeekly || retentionMonthly || retentionYearly;
  const effectiveRetention = `${retentionLatest || deviceConfig.retention.latest}/${retentionDaily || deviceConfig.retention.daily}/${retentionWeekly || deviceConfig.retention.weekly}/${retentionMonthly || deviceConfig.retention.monthly}/${retentionYearly || deviceConfig.retention.yearly}`;
  const effectiveRetentionSource = hasRetentionOverride ? 'Share' : 'Device';

  const hasPatternsOverride = includePatterns || excludePatterns;
  const effectiveIncludeCount = includePatterns ? includePatterns.split('\n').filter(p => p.trim()).length : 1;
  const effectiveExcludeCount = excludePatterns ? excludePatterns.split('\n').filter(p => p.trim()).length : 0;
  const effectivePatternsSource = hasPatternsOverride ? 'Share' : 'Device';

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div key={editingShare?.id ?? 'new'} className="w-full max-w-2xl bg-white rounded-lg shadow-xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="border-b border-gray-200 px-6 py-4 flex items-start justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">
              {isEditing ? `Edit Share: ${editingShare?.name}` : `Add Share to ${device.name}`}
            </h2>
            <p className="text-sm text-gray-600 mt-1">
              {isEditing 
                ? 'Update share configuration. Leave fields empty to inherit from device/global settings.'
                : 'Configure a new share to back up. Leave configuration fields empty to inherit from device/global settings.'
              }
            </p>
          </div>
          <button
            type="button"
            onClick={() => { resetForm(); onClose(); }}
            className="text-gray-400 hover:text-gray-600 transition-colors"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content - Single column layout */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {error && <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-md text-sm text-red-600">{error}</div>}

          <form onSubmit={handleSubmit} className="space-y-6">
              {/* Basic Information */}
              <div className="space-y-4">
                <h3 className="font-medium text-sm text-gray-900">Basic Information</h3>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Share Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={shareName}
                    onChange={(e) => setShareName(e.target.value)}
                    onBlur={() => setTouched(prev => ({ ...prev, shareName: true }))}
                    placeholder="e.g., Documents, Photos, Projects"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                  {validationErrors.shareName && (
                    <p className="text-xs text-red-600 mt-1">{validationErrors.shareName}</p>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    Share Path <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={sharePath}
                    onChange={(e) => setSharePath(e.target.value)}
                    onBlur={() => setTouched(prev => ({ ...prev, sharePath: true }))}
                    placeholder="/path/to/share or \\SERVER\Share"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  />
                  {validationErrors.sharePath && (
                    <p className="text-xs text-red-600 mt-1">{validationErrors.sharePath}</p>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                  <textarea
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="Optional description of what this share contains"
                    className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent min-h-[60px]"
                  />
                </div>
              </div>

              {/* Configuration Overrides */}
              <div className="space-y-4 pt-4 border-t">
                <h3 className="font-medium text-sm text-gray-900">Configuration Overrides</h3>
                <p className="text-sm text-gray-600">
                  Leave fields empty to inherit from device settings. Fill in values to override at the share level.
                </p>

                {/* Schedule */}
                <div>
                  <button
                    type="button"
                    onClick={() => setShowSchedule(!showSchedule)}
                    className="flex items-center gap-2 text-sm font-medium text-gray-700 w-full hover:text-gray-900"
                  >
                    {showSchedule ? (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M19 9l-7 7-7-7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    ) : (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M9 5l7 7-7 7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                    <span>Schedule</span>
                  </button>
                  {showSchedule && (
                    <div className="mt-3 pl-6 border-l-2 border-gray-200 space-y-2">
                      <label className="block text-sm font-medium text-gray-700">Share-Level Schedule</label>
                      <input
                        type="text"
                        value={schedule}
                        onChange={(e) => setSchedule(e.target.value)}
                        onBlur={() => setTouched(prev => ({ ...prev, schedule: true }))}
                        placeholder="Daily at 2:00 AM"
                        className={`w-full px-3 py-2 border rounded-md text-sm text-gray-900 font-mono focus:ring-2 focus:border-transparent ${validationErrors.schedule ? 'border-red-500 focus:ring-red-500' : 'border-gray-300 focus:ring-blue-500'}`}
                      />
                      <p className="text-xs text-gray-500">Leave empty to use device schedule. Use cron expression format.</p>
                      {cronDescription && !validationErrors.schedule && (
                        <p className="text-xs text-gray-600">Meaning: {cronDescription}</p>
                      )}
                      {validationErrors.schedule && (
                        <p className="text-xs text-red-600 mt-1">{validationErrors.schedule}</p>
                      )}
                    </div>
                  )}
                </div>

                {/* Retention Policy */}
                <div>
                  <button
                    type="button"
                    onClick={() => setShowRetention(!showRetention)}
                    className="flex items-center gap-2 text-sm font-medium text-gray-700 w-full hover:text-gray-900"
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
                    <div className="mt-3 pl-6 border-l-2 border-gray-200">
                      <div className="grid grid-cols-2 gap-4">
                        <div>
                          <label className="block text-xs font-medium text-gray-600 mb-1">Latest backups to keep</label>
                          <input
                            type="number"
                            value={retentionLatest}
                            onChange={(e) => setRetentionLatest(e.target.value)}
                            placeholder={deviceConfig.retention.latest.toString()}
                            className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-600 mb-1">Daily backups to keep</label>
                          <input
                            type="number"
                            value={retentionDaily}
                            onChange={(e) => setRetentionDaily(e.target.value)}
                            placeholder={deviceConfig.retention.daily.toString()}
                            className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-600 mb-1">Weekly backups to keep</label>
                          <input
                            type="number"
                            value={retentionWeekly}
                            onChange={(e) => setRetentionWeekly(e.target.value)}
                            placeholder={deviceConfig.retention.weekly.toString()}
                            className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-600 mb-1">Monthly backups to keep</label>
                          <input
                            type="number"
                            value={retentionMonthly}
                            onChange={(e) => setRetentionMonthly(e.target.value)}
                            placeholder={deviceConfig.retention.monthly.toString()}
                            className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          />
                        </div>
                        <div>
                          <label className="block text-xs font-medium text-gray-600 mb-1">Yearly backups to keep</label>
                          <input
                            type="number"
                            value={retentionYearly}
                            onChange={(e) => setRetentionYearly(e.target.value)}
                            placeholder={deviceConfig.retention.yearly.toString()}
                            className="w-full px-2 py-1 border border-gray-300 rounded text-sm text-gray-900 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                          />
                        </div>
                      </div>
                      <p className="text-xs text-gray-500 mt-2">Leave empty to use device defaults</p>
                    </div>
                  )}
                </div>

                {/* Include/Exclude Patterns */}
                <div>
                  <button
                    type="button"
                    onClick={() => setShowPatterns(!showPatterns)}
                    className="flex items-center gap-2 text-sm font-medium text-gray-700 w-full hover:text-gray-900"
                  >
                    {showPatterns ? (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M19 9l-7 7-7-7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    ) : (
                      <svg className="w-4 h-4 text-gray-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path d="M9 5l7 7-7 7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                    <span>Include/Exclude Patterns</span>
                  </button>
                  {showPatterns && (
                    <div className="mt-3 pl-6 border-l-2 border-gray-200 space-y-4">
                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Include Patterns</label>
                        <textarea
                          value={includePatterns}
                          onChange={(e) => setIncludePatterns(e.target.value)}
                          placeholder="*"
                          className="w-full px-2 py-1 border border-gray-300 rounded text-xs text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent h-20"
                        />
                        <p className="text-xs text-gray-500 mt-1">One pattern per line. Leave empty to use device patterns.</p>
                      </div>
                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-1">Exclude Patterns</label>
                        <textarea
                          value={excludePatterns}
                          onChange={(e) => setExcludePatterns(e.target.value)}
                          placeholder="*.tmp&#10;.git/**&#10;node_modules/**"
                          className="w-full px-2 py-1 border border-gray-300 rounded text-xs text-gray-900 font-mono focus:ring-2 focus:ring-blue-500 focus:border-transparent h-20"
                        />
                        <p className="text-xs text-gray-500 mt-1">One pattern per line. Leave empty to use device patterns.</p>
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* Configuration Hierarchy Breadcrumb */}
              <div className="flex items-center gap-2 p-3 bg-blue-50 rounded-lg border border-blue-200">
                <svg className="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M8 7v8a2 2 0 002 2h6M8 7V5a2 2 0 012-2h4.586a1 1 0 01.707.293l4.414 4.414a1 1 0 01.293.707V15a2 2 0 01-2 2h-2M8 7H6a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2v-2" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <div className="flex items-center gap-2 text-sm flex-wrap">
                  <span className="inline-block px-2 py-0.5 bg-white border border-gray-300 rounded text-xs font-medium text-gray-700">Global Defaults</span>
                  <span className="text-gray-500">→</span>
                  <span className="inline-block px-2 py-0.5 bg-blue-100 border border-blue-300 rounded text-xs font-medium text-blue-700">Device Settings</span>
                  <span className="text-gray-500">→</span>
                  <span className="inline-block px-2 py-0.5 bg-green-100 border border-green-300 rounded text-xs font-medium text-green-700">Share Settings (you are here)</span>
                </div>
              </div>

            {/* Effective Configuration Preview Panel */}
            <div className="p-4 bg-blue-50 border-2 border-blue-200 rounded-lg">
              <div className="flex items-center gap-2 mb-4">
                <svg className="w-4 h-4 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <h3 className="font-semibold text-sm text-gray-900">Effective Configuration</h3>
              </div>

              <div className="space-y-4 text-sm">
                {/* Schedule */}
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <div className="text-xs font-medium text-gray-600">Schedule</div>
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium border ${
                      effectiveScheduleSource === 'Share' 
                        ? 'bg-green-100 border-green-300 text-green-700'
                        : 'bg-blue-100 border-blue-300 text-blue-700'
                    }`}>
                      {effectiveScheduleSource}
                    </span>
                  </div>
                  <div className="text-gray-900 font-mono text-xs">{effectiveSchedule}</div>
                  {cronDescription && <div className="text-xs text-gray-600 mt-0.5">{cronDescription}</div>}
                </div>

                {/* Retention */}
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <div className="text-xs font-medium text-gray-600">Retention</div>
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium border ${
                      effectiveRetentionSource === 'Share'
                        ? 'bg-green-100 border-green-300 text-green-700'
                        : 'bg-blue-100 border-blue-300 text-blue-700'
                    }`}>
                      {effectiveRetentionSource}
                    </span>
                  </div>
                  <div className="text-gray-900 font-mono text-xs">{effectiveRetention}</div>
                  <div className="text-xs text-gray-600 mt-0.5">Latest/Daily/Weekly/Monthly/Yearly</div>
                </div>

                {/* Patterns */}
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <div className="text-xs font-medium text-gray-600">Patterns</div>
                    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium border ${
                      effectivePatternsSource === 'Share'
                        ? 'bg-green-100 border-green-300 text-green-700'
                        : 'bg-blue-100 border-blue-300 text-blue-700'
                    }`}>
                      {effectivePatternsSource}
                    </span>
                  </div>
                  <div className="text-gray-900 text-xs">
                    {effectiveIncludeCount} includes, {effectiveExcludeCount} excludes
                  </div>
                </div>
              </div>

              <div className="mt-4 pt-4 border-t border-blue-200 text-xs text-gray-600">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="inline-block px-2 py-0.5 bg-white border border-gray-300 rounded text-xs font-medium text-gray-700">Global</span>
                    <span>System defaults</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="inline-block px-2 py-0.5 bg-blue-100 border border-blue-300 rounded text-xs font-medium text-blue-700">Device</span>
                    <span>Device-level config</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="inline-block px-2 py-0.5 bg-green-100 border border-green-300 rounded text-xs font-medium text-green-700">Share</span>
                    <span>Share-level config</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Footer - moved inside form */}
            <div className="border-t border-gray-200 px-0 py-4 flex items-center justify-between bg-gray-50 -mx-6 px-6 mt-6">
              <button
                type="button"
                onClick={() => { resetForm(); onClose(); }}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={!isFormValid}
                className="px-4 py-2 text-sm font-medium text-white bg-blue-600 border border-transparent rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {editingShare ? 'Update Share' : 'Add Share'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
