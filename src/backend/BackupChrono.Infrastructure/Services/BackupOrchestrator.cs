using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<BackupOrchestrator> _logger;
    private readonly Dictionary<Guid, BackupJob> _activeJobs = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly Dictionary<Guid, (BackupJob Job, DateTime ExpiresAt)> _completedJobs = new();
    private readonly SemaphoreSlim _jobLock = new(1, 1);
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(1);

    public BackupOrchestrator(
        IDeviceService deviceService,
        IShareService shareService,
        IProtocolPluginLoader pluginLoader,
        IResticService resticService,
        IStorageMonitor storageMonitor,
        ILogger<BackupOrchestrator> logger)
    {
        _deviceService = deviceService;
        _shareService = shareService;
        _pluginLoader = pluginLoader;
        _resticService = resticService;
        _storageMonitor = storageMonitor;
        _logger = logger;
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
            }
            else if (backups.Count > 0)
            {
                job.Status = BackupJobStatus.PartiallyCompleted;
                var summary = $"Partially completed: {backups.Count}/{enabledShares.Count} shares backed up";
                job.ErrorMessage = string.IsNullOrWhiteSpace(job.ErrorMessage)
                    ? summary
                    : string.Concat(job.ErrorMessage.TrimEnd('\n'), "\n", summary);
                _logger.LogWarning("Device backup partially completed for '{DeviceName}' ({Completed}/{Total} shares)", device.Name, backups.Count, enabledShares.Count);
            }
            else
            {
                job.Status = BackupJobStatus.Failed;
                _logger.LogError("Device backup failed for '{DeviceName}' - all shares failed", device.Name);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Device backup cancelled for '{DeviceName}'", device.Name);
            job.Status = BackupJobStatus.Cancelled;
            job.ErrorMessage = "Backup cancelled by user";
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Device backup failed for '{DeviceName}'", device.Name);
            job.Status = BackupJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            cts.Dispose();
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
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Share backup cancelled for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            job.Status = BackupJobStatus.Cancelled;
            job.ErrorMessage = "Backup cancelled by user";
            job.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Share backup failed for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            job.Status = BackupJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            await UntrackJob(job.Id);
            cts.Dispose();
        }

        return job;
    }

    public async Task<BackupJob?> GetJobStatus(Guid jobId)
    {
        await _jobLock.WaitAsync();
        try
        {
            // Check active jobs first
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                return job;
            }

            // Check completed jobs (with TTL cleanup)
            CleanupExpiredCompletedJobs();
            if (_completedJobs.TryGetValue(jobId, out var completedEntry))
            {
                return completedEntry.Job;
            }

            return null;
        }
        finally
        {
            _jobLock.Release();
        }
    }

    public async Task CancelJob(Guid jobId)
    {
        await _jobLock.WaitAsync();
        try
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
                
                _logger.LogInformation("Backup job {JobId} cancellation requested", jobId);
            }
            else
            {
                _logger.LogWarning("Cannot cancel job {JobId} - job not found or already completed", jobId);
            }
        }
        finally
        {
            _jobLock.Release();
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

    public async Task<IEnumerable<BackupJob>> ListJobs()
    {
        await _jobLock.WaitAsync();
        try
        {
            CleanupExpiredCompletedJobs();
            var active = _activeJobs.Values;
            var completed = _completedJobs.Values.Select(c => c.Job);
            return active.Concat(completed).ToList();
        }
        finally
        {
            _jobLock.Release();
        }
    }

    private async Task<Backup> ExecuteShareBackupInternal(Device device, Share share, BackupJob job, CancellationToken cancellationToken)
    {
        var plugin = _pluginLoader.GetPlugin(device.Protocol);
        string? mountPath = null;

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
            }

            // Check cancellation before repository initialization
            cancellationToken.ThrowIfCancellationRequested();

            if (!await _resticService.RepositoryExists(repositoryPath))
            {
                _logger.LogInformation("Initializing new restic repository for '{DeviceName}/{ShareName}' at {RepositoryPath}", 
                    device.Name, share.Name, repositoryPath);
                
                var password = GetRepositoryPassword(device, share);
                await _resticService.InitializeRepository(repositoryPath, password);
                
                _logger.LogInformation("Repository initialized successfully");
            }

            // Check cancellation before backup
            cancellationToken.ThrowIfCancellationRequested();

            // Execute restic backup
            _logger.LogDebug("Starting restic backup for '{DeviceName}/{ShareName}'", device.Name, share.Name);
            var backup = await _resticService.CreateBackup(device, share, mountPath, rules);

            _logger.LogInformation("Backup completed with snapshot ID: {SnapshotId}", backup.Id);

            return backup;
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
            Type = jobType,
            Status = BackupJobStatus.Running,
            StartedAt = DateTime.UtcNow
        };
    }

    private async Task TrackJob(BackupJob job, CancellationTokenSource cts)
    {
        await _jobLock.WaitAsync();
        try
        {
            _activeJobs[job.Id] = job;
            _jobCancellationTokens[job.Id] = cts;
        }
        finally
        {
            _jobLock.Release();
        }
    }

    private async Task UntrackJob(Guid jobId)
    {
        await _jobLock.WaitAsync();
        try
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                // Move completed/failed jobs to completed store with TTL
                if (job.Status == BackupJobStatus.Completed || 
                    job.Status == BackupJobStatus.Failed || 
                    job.Status == BackupJobStatus.Cancelled)
                {
                    var expiresAt = DateTime.UtcNow.Add(CompletedJobRetention);
                    _completedJobs[jobId] = (job, expiresAt);
                    _logger.LogDebug("Job {JobId} moved to completed store, expires at {ExpiresAt}", 
                        jobId, expiresAt);
                }
                
                _activeJobs.Remove(jobId);
            }
            
            // Clean up and dispose the cancellation token source
            if (_jobCancellationTokens.TryGetValue(jobId, out var cts))
            {
                _jobCancellationTokens.Remove(jobId);
                cts.Dispose();
            }
        }
        finally
        {
            _jobLock.Release();
        }
    }

    /// <summary>
    /// Removes expired completed jobs from the completed jobs store.
    /// Must be called while holding _jobLock.
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
            _completedJobs.Remove(jobId);
            _logger.LogDebug("Removed expired completed job {JobId}", jobId);
        }
    }

    private string GetRepositoryPath(Device device, Share share)
    {
        // TODO: Make repository base path configurable
        // For now, use a local path structure
        return Path.Combine("./repositories", device.Id.ToString(), share.Id.ToString());
    }

    private string GetRepositoryPassword(Device device, Share share)
    {
        // Password comes from the device configuration
        if (device.Password == null)
        {
            throw new InvalidOperationException($"No password configured for device '{device.Name}'");
        }
        
        return device.Password.GetPlaintext();
    }
}
