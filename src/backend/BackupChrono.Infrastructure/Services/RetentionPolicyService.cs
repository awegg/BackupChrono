using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Orchestrates retention policy application across devices and shares.
/// Resolves effective policy from global → device → share cascade.
/// </summary>
public class RetentionPolicyService : IRetentionPolicyService
{
    private readonly IResticService _resticService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGlobalConfigRepository _globalConfigRepository;
    private readonly ILogger<RetentionPolicyService> _logger;
    private readonly string _logFilePath;
    private readonly string _lastRunFilePath;

    private RetentionLastRun? _lastRun = null;
    private readonly object _lastRunLock = new();

    public RetentionPolicyService(
        IResticService resticService,
            IServiceScopeFactory scopeFactory,
        IGlobalConfigRepository globalConfigRepository,
        IConfiguration configuration,
        ILogger<RetentionPolicyService> logger)
    {
        _resticService = resticService;
            _scopeFactory = scopeFactory;
        _globalConfigRepository = globalConfigRepository;
        _logger = logger;
        
        // Get log directory from configuration, default to config directory
        var logDirectory = configuration.GetValue<string>("RetentionPolicy:LogDirectory") 
            ?? Path.Combine(configuration.GetValue<string>("ConfigRepository:Path") ?? "./config", "logs");
        Directory.CreateDirectory(logDirectory);
        
        _logFilePath = Path.Combine(logDirectory, "retention-policy.jsonl");
        _lastRunFilePath = Path.Combine(logDirectory, "retention-last-run.json");
        
        // Load last run from file on startup (synchronous to ensure it completes before constructor returns)
        LoadLastRunFromFileSync();
    }

    public async Task<IEnumerable<RetentionRunResult>> ApplyForAll(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting retention policy application for all devices (dry-run={DryRun})", dryRun);
        var results = new List<RetentionRunResult>();
        var totalPruned = 0;
        long totalReclaimed = 0;

        try
        {
                using var scope = _scopeFactory.CreateScope();
                var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
                var devices = await deviceService.ListDevices();
            foreach (var device in devices)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Retention policy application cancelled");
                    break;
                }

                var deviceResults = await ApplyForDevice(device.Name, dryRun, cancellationToken);
                results.AddRange(deviceResults);
                totalPruned += deviceResults.Sum(r => r.SnapshotsPruned);
                totalReclaimed += deviceResults.Sum(r => r.SpaceReclaimedBytes);
            }

            // Update last run summary
            lock (_lastRunLock)
            {
                _lastRun = new RetentionLastRun
                {
                    Timestamp = DateTime.UtcNow,
                    TotalSnapshotsPruned = totalPruned,
                    TotalSpaceReclaimedBytes = totalReclaimed
                };
            }

            // Persist last run to file
            await SaveLastRunToFile(_lastRun);

            _logger.LogInformation(
                "Retention policy application completed: {Devices} devices, {Pruned} snapshots pruned, {Reclaimed} bytes reclaimed",
                results.Select(r => r.DeviceName).Distinct().Count(),
                totalPruned,
                totalReclaimed);

            // Write results to JSONL log
            await WriteRetentionLog(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retention policy application failed");
            throw;
        }

        return results;
    }

    public async Task<IEnumerable<RetentionRunResult>> ApplyForDevice(string deviceName, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying retention policy for device: {DeviceName} (dry-run={DryRun})", deviceName, dryRun);
        var results = new List<RetentionRunResult>();

            using var scope = _scopeFactory.CreateScope();
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            var shareService = scope.ServiceProvider.GetRequiredService<IShareService>();
        
            var device = await deviceService.GetDeviceByName(deviceName);
        if (device == null)
        {
            _logger.LogWarning("Device not found: {DeviceName}", deviceName);
            return results;
        }

            var shares = await shareService.ListShares(device.Id);
        var globalPolicy = await GetGlobalRetentionPolicy();

        // If device has no shares, apply device-level retention
        if (!shares.Any())
        {
            var effectivePolicy = device.RetentionPolicy ?? globalPolicy;
            if (effectivePolicy != null && effectivePolicy.IsValid())
            {
                var result = await ApplyRetentionForScope(deviceName, null, effectivePolicy, dryRun, cancellationToken);
                results.Add(result);
            }
            else
            {
                _logger.LogWarning("No valid retention policy for device: {DeviceName}", deviceName);
            }
            return results;
        }

        // Apply retention for each enabled share
        foreach (var share in shares.Where(s => s.Enabled))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Retention policy cancelled for device: {DeviceName}", deviceName);
                break;
            }

            // Cascade: share → device → global
            var effectivePolicy = share.RetentionPolicy ?? device.RetentionPolicy ?? globalPolicy;
            if (effectivePolicy == null || !effectivePolicy.IsValid())
            {
                _logger.LogWarning("No valid retention policy for share: {DeviceName}/{ShareName}", deviceName, share.Name);
                continue;
            }

            var result = await ApplyRetentionForScope(deviceName, share.Name, effectivePolicy, dryRun, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public Task<RetentionLastRun?> GetLastRun()
    {
        lock (_lastRunLock)
        {
            return Task.FromResult(_lastRun);
        }
    }

    /// <summary>
    /// Applies retention policy for a specific device/share scope.
    /// </summary>
    private async Task<RetentionRunResult> ApplyRetentionForScope(
        string deviceName,
        string? shareName,
        RetentionPolicy policy,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var scope = shareName != null ? $"{deviceName}/{shareName}" : deviceName;
        _logger.LogInformation(
            "Applying retention policy for {Scope}: keep-latest={Latest}, keep-daily={Daily}, keep-weekly={Weekly}, keep-monthly={Monthly}, keep-yearly={Yearly}",
            scope, policy.KeepLatest, policy.KeepDaily, policy.KeepWeekly, policy.KeepMonthly, policy.KeepYearly);

        try
        {
            if (dryRun)
            {
                _logger.LogInformation("Dry-run mode: calling restic with --dry-run for {Scope}", scope);
            }

            // Call ResticService to apply retention
            var (snapshotsPruned, spaceReclaimed) = await _resticService.ApplyRetentionPolicy(deviceName, shareName, policy, dryRun);

            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            _logger.LogInformation("Retention policy applied successfully for {Scope} in {Duration:F2}s: {Pruned} snapshots pruned, {Space} bytes reclaimed", 
                scope, duration.TotalSeconds, snapshotsPruned, spaceReclaimed);

            return new RetentionRunResult
            {
                DeviceName = deviceName,
                ShareName = shareName,
                StartTime = startTime,
                EndTime = endTime,
                Duration = duration,
                SnapshotsPruned = snapshotsPruned,
                SpaceReclaimedBytes = spaceReclaimed,
                Error = null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply retention policy for {Scope}", scope);
            return new RetentionRunResult
            {
                DeviceName = deviceName,
                ShareName = shareName,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                SnapshotsPruned = 0,
                SpaceReclaimedBytes = 0,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Reads global retention policy from global.yaml via repository.
    /// </summary>
    private async Task<RetentionPolicy?> GetGlobalRetentionPolicy()
    {
        return await _globalConfigRepository.GetDefaultRetentionPolicy();
    }

    /// <summary>
    /// Loads last run summary from file.
    /// </summary>
    private void LoadLastRunFromFileSync()
    {
        try
        {
            if (File.Exists(_lastRunFilePath))
            {
                var json = File.ReadAllText(_lastRunFilePath);
                var lastRun = JsonSerializer.Deserialize<RetentionLastRun>(json);
                lock (_lastRunLock)
                {
                    _lastRun = lastRun;
                }
                _logger.LogInformation("Loaded last retention run from file: {Timestamp}", lastRun?.Timestamp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load last retention run from file");
        }
    }

    /// <summary>
    /// Saves last run summary to file.
    /// </summary>
    private async Task SaveLastRunToFile(RetentionLastRun lastRun)
    {
        try
        {
            var json = JsonSerializer.Serialize(lastRun, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_lastRunFilePath, json);
            _logger.LogDebug("Saved last retention run to file: {FilePath}", _lastRunFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save last retention run to file");
        }
    }

    /// <summary>
    /// Writes retention run results to JSONL log file.
    /// </summary>
    private async Task WriteRetentionLog(IEnumerable<RetentionRunResult> results)
    {
        try
        {
            var logLines = results.Select(result =>
            {
                var logEntry = new
                {
                    timestamp = DateTime.UtcNow,
                    deviceName = result.DeviceName,
                    shareName = result.ShareName,
                    startTime = result.StartTime,
                    endTime = result.EndTime,
                    durationSeconds = result.Duration.TotalSeconds,
                    snapshotsPruned = result.SnapshotsPruned,
                    spaceReclaimedBytes = result.SpaceReclaimedBytes,
                    success = string.IsNullOrWhiteSpace(result.Error),
                    error = result.Error
                };
                return JsonSerializer.Serialize(logEntry);
            }).ToList();

            if (logLines.Any())
            {
                await File.AppendAllLinesAsync(_logFilePath, logLines);
            }

            _logger.LogDebug("Written {Count} retention results to {LogFile}", results.Count(), _logFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write retention log to {LogFile}", _logFilePath);
        }
    }
}
