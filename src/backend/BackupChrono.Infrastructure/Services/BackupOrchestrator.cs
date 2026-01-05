using System.Collections.Concurrent;
using System.Security.Cryptography;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Core.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Orchestrates the complete backup workflow including device wake, protocol mounting,
/// restic execution, and unmounting.
/// </summary>
public class BackupOrchestrator : IBackupOrchestrator
{
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly IProtocolPluginLoader _pluginLoader;
    private readonly IResticService _resticService;
    private readonly IStorageMonitor _storageMonitor;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupLogService _backupLogService;
    private readonly ILogger<BackupOrchestrator> _logger;
    private readonly string _repositoryBasePath;
    private readonly ConcurrentDictionary<Guid, BackupJob> _activeJobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly ConcurrentDictionary<Guid, (BackupJob Job, DateTime ExpiresAt)> _completedJobs = new();
    private readonly ConcurrentDictionary<string, (double LastPercent, DateTime LastBroadcast)> _progressThrottle = new();
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProgressBroadcastInterval = TimeSpan.FromMilliseconds(500);
    private const double ProgressPercentThreshold = 1.0;

    public event EventHandler<BackupProgress>? ProgressUpdated;

    public BackupOrchestrator(
        IDeviceService deviceService,
        IShareService shareService,
        IProtocolPluginLoader pluginLoader,
        IResticService resticService,
        IStorageMonitor storageMonitor,
        IBackupJobRepository backupJobRepository,
        IBackupLogService backupLogService,
        ILogger<BackupOrchestrator> logger,
        IOptions<ResticOptions> resticOptions)
    {
        _deviceService = deviceService;
        _shareService = shareService;
        _pluginLoader = pluginLoader;
        _resticService = resticService;
        _storageMonitor = storageMonitor;
        _backupJobRepository = backupJobRepository;
        _backupLogService = backupLogService;
        _logger = logger;
        _repositoryBasePath = resticOptions.Value.RepositoryBasePath;
    }

    private void RaiseProgressUpdate(BackupJob job, string? currentFile = null, double? percentComplete = null)
    {
        ProgressUpdated?.Invoke(this, new BackupProgress
        {
            JobId = job.Id.ToString(),
            DeviceName = job.DeviceName,
            ShareName = job.ShareName,
            Status = job.Status.ToString(),
            PercentComplete = percentComplete ?? 0,
            CurrentFile = currentFile,
            ErrorMessage = job.ErrorMessage
        });
    }

    private void RaiseProgressUpdate(BackupProgress progress)
    {
        var now = DateTime.UtcNow;
        var shouldBroadcast = false;

        // Check if we should throttle this broadcast
        if (_progressThrottle.TryGetValue(progress.JobId, out var lastState))
        {
            var timeSinceLastBroadcast = now - lastState.LastBroadcast;
            var percentChanged = Math.Abs(progress.PercentComplete - lastState.LastPercent);

            // Broadcast if percent changed significantly OR enough time has passed
            shouldBroadcast = percentChanged >= ProgressPercentThreshold || 
                             timeSinceLastBroadcast >= ProgressBroadcastInterval;
        }
        else
        {
            // First broadcast for this job
            shouldBroadcast = true;
        }

        if (shouldBroadcast)
        {
            _progressThrottle[progress.JobId] = (progress.PercentComplete, now);
            ProgressUpdated?.Invoke(this, progress);
        }
    }

    public async Task<BackupJob> ExecuteDeviceBackup(Guid deviceId, BackupJobType jobType)
    {
        var device = await _deviceService.GetDevice(deviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{deviceId}' not found.");
        }

        var shares = await _shareService.ListShares(deviceId);
        var enabledShares = shares.Where(s => s.Enabled).ToList();

        if (!enabledShares.Any())
        {
            throw new InvalidOperationException($"Device '{device.Name}' has no enabled shares to backup.");
        }

        var job = CreateBackupJob(device, null, jobType);
        var cts = new CancellationTokenSource();
        await TrackJob(job, cts);

        try
        {
            _logger.LogInformation("Starting device-level backup for '{DeviceName}' (Job: {JobId})", device.Name, job.Id);
            RaiseProgressUpdate(job, percentComplete: 0);

            // Wake device if needed
            if (device.WakeOnLanEnabled)
            {
                await WakeDevice(device, job, cts.Token);
            }

            // Backup each enabled share
            var backups = new List<Backup>();
            foreach (var share in enabledShares)
            {
                cts.Token.ThrowIfCancellationRequested();
                
                try
                {
                    var backup = await ExecuteShareBackupInternal(device, share, job, cts.Token);
                    backups.Add(backup);
                    
                    // For device-level backups, store the last successful backup ID
                    // (in multi-share device backups, this will be the last share's snapshot)
                    job.BackupId = backup.Id;
                    job.ShareId = share.Id; // Track which share this backup belongs to
                    job.ShareName = share.Name;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Backup cancelled for share '{ShareName}' on device '{DeviceName}'", share.Name, device.Name);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to backup share '{ShareName}' on device '{DeviceName}'", share.Name, device.Name);
                    job.ErrorMessage += $"Share '{share.Name}' failed: {ex.Message}\n";
                }
            }

            // Update job status
            job.CompletedAt = DateTime.UtcNow;

            if (backups.Count == enabledShares.Count)
            {
                job.Status = BackupJobStatus.Completed;
                _logger.LogInformation("Device backup completed successfully for '{DeviceName}'", device.Name);
                RaiseProgressUpdate(job, percentComplete: 100);
            }
            else if (backups.Count > 0)
            {
                job.Status = BackupJobStatus.PartiallyCompleted;
                var summary = $"Partially completed: {backups.Count}/{enabledShares.Count} shares backed up";
                job.ErrorMessage = string.IsNullOrWhiteSpace(job.ErrorMessage)
                    ? summary
                    : string.Concat(job.ErrorMessage.TrimEnd('\n'), "\n", summary);
                _logger.LogWarning("Device backup partially completed for '{DeviceName}' ({Completed}/{Total} shares)", device.Name, backups.Count, enabledShares.Count);
                RaiseProgressUpdate(job, percentComplete: 100);
            }
            else
            {
                job.Status = BackupJobStatus.Failed;
                _logger.LogError("Device backup failed for '{DeviceName}' - all shares failed", device.Name);
                RaiseProgressUpdate(job);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Device backup cancelled for '{DeviceName}'", device.Name);
            // Only update status if not already cancelled (may have been cancelled externally)
            if (job.Status != BackupJobStatus.Cancelled)
            {
                job.Status = BackupJobStatus.Cancelled;
                job.ErrorMessage = "Backup cancelled by user";
            }
            job.CompletedAt = DateTime.UtcNow;
            RaiseProgressUpdate(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device backup failed for '{DeviceName}'", device.Name);
            // Only update status if not already finalised (may have been cancelled externally)
            if (job.Status != BackupJobStatus.Cancelled)
            {
                job.Status = BackupJobStatus.Failed;
                job.ErrorMessage = ex.Message;
            }
            job.CompletedAt = DateTime.UtcNow;
            RaiseProgressUpdate(job);
        }
        finally
        {
            await UntrackJob(job.Id);
        }

        return job;
    }

    public async Task<BackupJob> ExecuteShareBackup(Guid deviceId, Guid shareId, BackupJobType jobType)
    {
        var device = await _deviceService.GetDevice(deviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{deviceId}' not found.");
        }

        var share = await _shareService.GetShare(shareId);
        if (share == null)
        {
            throw new InvalidOperationException($"Share with ID '{shareId}' not found.");
        }

        if (share.DeviceId != deviceId)
        {
            throw new InvalidOperationException($"Share '{share.Name}' does not belong to device '{device.Name}'.");
        }

        if (!share.Enabled)
        {
            throw new InvalidOperationException($"Share '{share.Name}' is disabled.");
        }

        var job = CreateBackupJob(device, share, jobType);
        var cts = new CancellationTokenSource();
        await TrackJob(job, cts);

        try
        {
            _logger.LogInformation("Starting share-level backup for '{DeviceName}/{ShareName}' (Job: {JobId})", device.Name, share.Name, job.Id);
            RaiseProgressUpdate(job, percentComplete: 0);

            // Wake device if needed
            if (device.WakeOnLanEnabled)
            {
                await WakeDevice(device, job, cts.Token);
            }

            // Execute backup
            var backup = await ExecuteShareBackupInternal(device, share, job, cts.Token);

            job.CompletedAt = DateTime.UtcNow;
            job.BackupId = backup.Id;
            job.Status = BackupJobStatus.Completed;

            _logger.LogInformation("Share backup completed successfully for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            RaiseProgressUpdate(job, percentComplete: 100);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Share backup cancelled for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            // Only update status if not already cancelled (may have been cancelled externally)
            if (job.Status != BackupJobStatus.Cancelled)
            {
                job.Status = BackupJobStatus.Cancelled;
                job.ErrorMessage = "Backup cancelled by user";
            }
            job.CompletedAt = DateTime.UtcNow;
            RaiseProgressUpdate(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Share backup failed for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            // Only update status if not already finalised (may have been cancelled externally)
            if (job.Status != BackupJobStatus.Cancelled)
            {
                job.Status = BackupJobStatus.Failed;
                job.ErrorMessage = ex.Message;
            }
            job.CompletedAt = DateTime.UtcNow;
            RaiseProgressUpdate(job);
        }
        finally
        {
            await UntrackJob(job.Id);
        }

        return job;
    }

    public Task<BackupJob?> GetJobStatus(Guid jobId)
    {
        // Check active jobs first
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult<BackupJob?>(job);
        }

        // Check completed jobs (with TTL cleanup)
        CleanupExpiredCompletedJobs();
        if (_completedJobs.TryGetValue(jobId, out var completedEntry))
        {
            return Task.FromResult<BackupJob?>(completedEntry.Job);
        }

        return Task.FromResult<BackupJob?>(null);
    }

    public async Task CancelJob(Guid jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job) && 
            _jobCancellationTokens.TryGetValue(jobId, out var cts))
        {
            _logger.LogInformation("Cancelling backup job {JobId}", jobId);
            
            // Trigger cancellation
            cts.Cancel();
            
            // Update job status immediately
            job.Status = BackupJobStatus.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = "Backup cancelled by user";
            
            // Persist cancelled status immediately
            await _backupJobRepository.SaveJob(job);
            
            _logger.LogInformation("Backup job {JobId} cancellation requested and persisted", jobId);
        }
        else
        {
            _logger.LogWarning("Cannot cancel job {JobId} - job not found or already completed", jobId);
        }
    }

    public async Task<BackupJob> RetryFailedJob(Guid jobId)
    {
        var job = await GetJobStatus(jobId);
        if (job == null)
        {
            throw new InvalidOperationException($"Job with ID '{jobId}' not found.");
        }

        if (job.Status != BackupJobStatus.Failed)
        {
            throw new InvalidOperationException($"Job {jobId} is not in Failed status (current: {job.Status}).");
        }

        // Execute retry based on job target
        if (job.ShareId.HasValue)
        {
            return await ExecuteShareBackup(job.DeviceId, job.ShareId.Value, BackupJobType.Retry);
        }
        else
        {
            return await ExecuteDeviceBackup(job.DeviceId, BackupJobType.Retry);
        }
    }

    public Task<IEnumerable<BackupJob>> ListJobs()
    {
        CleanupExpiredCompletedJobs();
        var active = _activeJobs.Values;
        var completed = _completedJobs.Values.Select(c => c.Job);
        return Task.FromResult(active.Concat(completed).ToList().AsEnumerable());
    }

    private async Task<Backup> ExecuteShareBackupInternal(Device device, Share share, BackupJob job, CancellationToken cancellationToken)
    {
        var plugin = _pluginLoader.GetPlugin(device.Protocol);
        string? mountPath = null;
        var progressEntries = new List<ProgressLogEntry>();
        var warnings = new List<string>();
        var errors = new List<string>();

        try
        {
            // Check cancellation before mounting
            cancellationToken.ThrowIfCancellationRequested();
            
            // Mount share
            _logger.LogDebug("Mounting share '{ShareName}' on device '{DeviceName}'", share.Name, device.Name);
            mountPath = await plugin.MountShare(device, share);

            if (string.IsNullOrEmpty(mountPath))
            {
                throw new InvalidOperationException($"Failed to mount share '{share.Name}' - plugin returned null or empty path.");
            }

            _logger.LogDebug("Share mounted at: {MountPath}", mountPath);

            // Check cancellation before proceeding
            cancellationToken.ThrowIfCancellationRequested();

            // Get effective rules (configuration cascade: share > device > global)
            var rules = share.IncludeExcludeRules ?? device.IncludeExcludeRules ?? new IncludeExcludeRules();

            // Initialize repository if it doesn't exist
            var repositoryPath = GetRepositoryPath(device, share);
            
            // Check storage capacity before proceeding
            var storageStatus = await _storageMonitor.GetStorageStatus(repositoryPath);
            if (storageStatus.ThresholdLevel == StorageThresholdLevel.Exhausted)
            {
                _logger.LogError(
                    "Backup paused: Storage exhausted at {Path}. {Message}",
                    repositoryPath, storageStatus.Message);
                throw new InvalidOperationException(
                    $"Backup cannot proceed: {storageStatus.Message}. Please free up disk space.");
            }
            
            if (storageStatus.ThresholdLevel == StorageThresholdLevel.Critical)
            {
                _logger.LogWarning(
                    "Storage critical at {Path}: {Message}. Backup will proceed but may fail.",
                    repositoryPath, storageStatus.Message);
                warnings.Add($"Storage critical: {storageStatus.Message}");
            }

            // Check cancellation before repository initialization
            cancellationToken.ThrowIfCancellationRequested();

            if (!await _resticService.RepositoryExists(repositoryPath))
            {
                _logger.LogInformation("Initializing new restic repository for '{DeviceName}/{ShareName}' at {RepositoryPath}", 
                    device.Name, share.Name, repositoryPath);
                
                var password = await GetRepositoryPassword(device, share);
                await _resticService.InitializeRepository(repositoryPath, password);
                
                _logger.LogInformation("Repository initialized successfully");
            }

            // Check cancellation before backup
            cancellationToken.ThrowIfCancellationRequested();

            // Build command line for debugging (includes repository path, excludes password)
            var includeExcludeArgs = string.Join(" ", rules.ExcludePatterns.Select(p => $"--exclude \"{p}\""));
            job.CommandLine = $"RESTIC_REPOSITORY={repositoryPath}\nrestic backup \"{mountPath}\" --json {includeExcludeArgs}";
            await _backupJobRepository.SaveJob(job);

            // Execute restic backup
            _logger.LogDebug("Starting restic backup for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            
            // Track final progress values
            BackupProgress? lastProgress = null;
            
            var backup = await _resticService.CreateBackup(repositoryPath, device, share, mountPath, rules, progress =>
            {
                // Create a new progress object with the JobId set
                var updatedProgress = new BackupProgress
                {
                    JobId = job.Id.ToString(),
                    DeviceName = progress.DeviceName,
                    ShareName = progress.ShareName,
                    Status = progress.Status,
                    FilesProcessed = progress.FilesProcessed,
                    TotalFiles = progress.TotalFiles,
                    BytesProcessed = progress.BytesProcessed,
                    TotalBytes = progress.TotalBytes,
                    PercentComplete = progress.PercentComplete,
                    CurrentFile = progress.CurrentFile,
                    ErrorMessage = progress.ErrorMessage
                };
                lastProgress = updatedProgress;
                progressEntries.Add(ToProgressLogEntry(updatedProgress));
                RaiseProgressUpdate(updatedProgress);
            },
            warning =>
            {
                warnings.Add(warning);
                _logger.LogWarning("Restic warning for job {JobId}: {Warning}", job.Id, warning);
            },
            error =>
            {
                errors.Add(error);
                _logger.LogError("Restic error for job {JobId}: {Error}", job.Id, error);
            },
            cancellationToken);

            // Save final statistics to job
            if (lastProgress != null)
            {
                job.FilesProcessed = lastProgress.FilesProcessed;
                job.BytesTransferred = lastProgress.BytesProcessed;
            }

            _logger.LogInformation("Backup completed with snapshot ID: {SnapshotId}", backup.Id);
            
            // Link job and backup
            backup.CreatedByJobId = job.Id;

            // Persist captured logs against the backup
            progressEntries.Add(new ProgressLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Message = "Backup completed",
                PercentDone = 100,
                FilesDone = lastProgress?.FilesProcessed ?? 0,
                BytesDone = lastProgress?.BytesProcessed ?? 0,
                CurrentFile = null
            });
            await PersistBackupLog(backup.Id, job, progressEntries, warnings, errors);

            return backup;
        }
        catch (Exception ex)
        {
            // Persist whatever we captured so far under the job ID when snapshot ID is unavailable
            errors.Add(ex.Message);
            var logKey = job.BackupId ?? job.Id.ToString();
            await PersistBackupLog(logKey, job, progressEntries, warnings, errors);
            throw;
        }
        finally
        {
            // Unmount share
            if (!string.IsNullOrEmpty(mountPath))
            {
                try
                {
                    _logger.LogDebug("Unmounting share '{ShareName}'", share.Name);
                    await plugin.UnmountShare(mountPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to unmount share '{ShareName}' at '{MountPath}'", share.Name, mountPath);
                }
            }
        }
    }

    private async Task WakeDevice(Device device, BackupJob job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(device.WakeOnLanMacAddress))
        {
            _logger.LogWarning("Wake-on-LAN enabled for '{DeviceName}' but MAC address is missing", device.Name);
            return;
        }

        try
        {
            _logger.LogInformation("Sending Wake-on-LAN packet to '{DeviceName}' ({MacAddress})", device.Name, device.WakeOnLanMacAddress);
            var plugin = _pluginLoader.GetPlugin(device.Protocol);
            await plugin.WakeDevice(device);

            // Wait for device to wake up (TODO: make configurable)
            _logger.LogDebug("Waiting 30 seconds for device to wake up...");
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Wake-on-LAN cancelled for '{DeviceName}'", device.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wake-on-LAN failed for '{DeviceName}'", device.Name);
            // Continue anyway - device might already be awake
        }
    }

    private BackupJob CreateBackupJob(Device device, Share? share, BackupJobType jobType)
    {
        return new BackupJob
        {
            DeviceId = device.Id,
            ShareId = share?.Id,
            DeviceName = device.Name,
            ShareName = share?.Name,
            Type = jobType,
            Status = BackupJobStatus.Running,
            StartedAt = DateTime.UtcNow
        };
    }

    private async Task TrackJob(BackupJob job, CancellationTokenSource cts)
    {
        _activeJobs[job.Id] = job;
        _jobCancellationTokens[job.Id] = cts;
        
        // Persist job immediately so it appears in the jobs list even while running
        await _backupJobRepository.SaveJob(job);
        _logger.LogDebug("Job {JobId} created and persisted with status {Status}", job.Id, job.Status);
    }

    private async Task UntrackJob(Guid jobId)
    {
        if (_activeJobs.TryRemove(jobId, out var job))
        {
            // Clean up progress throttle cache
            _progressThrottle.TryRemove(jobId.ToString(), out _);
            
            // Always persist final status to repository
            await _backupJobRepository.SaveJob(job);
            _logger.LogInformation("Job {JobId} completed with status {Status}", jobId, job.Status);
            
            // Move completed/failed jobs to completed store with TTL for quick in-memory access
            if (job.Status == BackupJobStatus.Completed || 
                job.Status == BackupJobStatus.Failed || 
                job.Status == BackupJobStatus.Cancelled ||
                job.Status == BackupJobStatus.PartiallyCompleted)
            {
                var expiresAt = DateTime.UtcNow.Add(CompletedJobRetention);
                _completedJobs[jobId] = (job, expiresAt);
                _logger.LogDebug("Job {JobId} moved to completed store, expires at {ExpiresAt}", 
                    jobId, expiresAt);
            }
        }
        
        // Clean up and dispose the cancellation token source
        if (_jobCancellationTokens.TryRemove(jobId, out var cts))  {
            cts.Dispose();
        }
    }

    /// <summary>
    /// Removes expired completed jobs from the completed jobs store.
    /// </summary>
    private void CleanupExpiredCompletedJobs()
    {
        var now = DateTime.UtcNow;
        var expiredJobIds = _completedJobs
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var jobId in expiredJobIds)
        {
            _completedJobs.TryRemove(jobId, out _);
            _logger.LogDebug("Removed expired completed job {JobId}", jobId);
        }
    }

    private string GetRepositoryPath(Device device, Share share)
    {
        // Build repository path: {RepositoryBasePath}/{deviceId}/{shareId}
        // Use injected base path rather than relying on current working directory
        return Path.Combine(_repositoryBasePath, device.Id.ToString(), share.Id.ToString());
    }

    private async Task<string> GetRepositoryPassword(Device device, Share share)
    {
        // Prefer explicit repository credential when provided
        if (share.RepositoryPassword != null)
        {
            var explicitPassword = share.RepositoryPassword.GetPlaintext();
            if (!string.IsNullOrWhiteSpace(explicitPassword))
            {
                return explicitPassword;
            }
        }

        var devicePassword = device.Password?.GetPlaintext();
        if (string.IsNullOrWhiteSpace(devicePassword))
        {
            throw new InvalidOperationException(
                $"No repository password configured for share '{share.Name}' and no device credential available to derive one for device '{device.Name}'.");
        }

        var salt = EnsureRepositorySalt(share);
        var derivedPassword = DeriveRepositoryKey(devicePassword, salt);

        // Persist the derived credential encrypted to avoid plaintext storage
        share.RepositoryPassword = new EncryptedCredential(derivedPassword);
        share.RepositoryKeySalt = salt;
        await _shareService.UpdateShare(share);

        return derivedPassword;
    }

    private static string EnsureRepositorySalt(Share share)
    {
        if (!string.IsNullOrWhiteSpace(share.RepositoryKeySalt))
        {
            return share.RepositoryKeySalt;
        }

        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var salt = Convert.ToBase64String(saltBytes);
        share.RepositoryKeySalt = salt;
        return salt;
    }

    private static string DeriveRepositoryKey(string devicePassword, string saltBase64)
    {
        var saltBytes = Convert.FromBase64String(saltBase64);
        using var pbkdf2 = new Rfc2898DeriveBytes(devicePassword, saltBytes, 150_000, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(32); // 256-bit key
        return Convert.ToBase64String(key);
    }

    public async Task TrackFailedJob(BackupJob failedJob)
    {
        // Save to repository so it persists and appears in the jobs list
        await _backupJobRepository.SaveJob(failedJob);
        
        // Also track in-memory for quick access
        var expiresAt = DateTime.UtcNow.Add(CompletedJobRetention);
        _completedJobs[failedJob.Id] = (failedJob, expiresAt);
        
        _logger.LogInformation(
            "Tracked failed job {JobId} with error: {Error}",
            failedJob.Id,
            failedJob.ErrorMessage);
    }

    public int GetActiveJobCount()
    {
        return _activeJobs.Count;
    }

    public async Task CancelAllJobs()
    {
        var activeJobs = _activeJobs.Values.ToList();
        _logger.LogWarning("Cancelling all {Count} active backup jobs", activeJobs.Count);
        
        foreach (var job in activeJobs)
        {
            await CancelJob(job.Id);
        }
    }

    private static ProgressLogEntry ToProgressLogEntry(BackupProgress progress)
    {
        return new ProgressLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Message = progress.CurrentFile ?? "Progress update",
            PercentDone = (int)Math.Round(progress.PercentComplete),
            CurrentFile = progress.CurrentFile,
            FilesDone = progress.FilesProcessed,
            BytesDone = progress.BytesProcessed
        };
    }

    private async Task PersistBackupLog(string backupId, BackupJob job, List<ProgressLogEntry> progressEntries, List<string> warnings, List<string> errors)
    {
        await _backupLogService.GetOrCreateLog(backupId, job.Id.ToString());

        foreach (var entry in progressEntries)
        {
            await _backupLogService.AddProgressEntry(backupId, entry);
        }

        foreach (var warning in warnings)
        {
            await _backupLogService.AddWarning(backupId, warning);
        }

        foreach (var error in errors)
        {
            await _backupLogService.AddError(backupId, error);
        }
    }
}
