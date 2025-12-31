using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Orchestrates the complete backup workflow including device wake, protocol mounting,
/// restic execution, and unmounting.
/// </summary>
public interface IBackupOrchestrator
{
    /// <summary>
    /// Executes a device-level backup (all shares).
    /// </summary>
    /// <param name="deviceId">Device to backup.</param>
    /// <param name="jobType">Type of backup job (Scheduled, Manual, Retry).</param>
    /// <returns>Created backup job.</returns>
    Task<BackupJob> ExecuteDeviceBackup(Guid deviceId, BackupJobType jobType);

    /// <summary>
    /// Executes a share-level backup (single share).
    /// </summary>
    /// <param name="deviceId">Device containing the share.</param>
    /// <param name="shareId">Share to backup.</param>
    /// <param name="jobType">Type of backup job (Scheduled, Manual, Retry).</param>
    /// <returns>Created backup job.</returns>
    Task<BackupJob> ExecuteShareBackup(Guid deviceId, Guid shareId, BackupJobType jobType);

    /// <summary>
    /// Gets the status of a backup job.
    /// </summary>
    Task<BackupJob?> GetJobStatus(Guid jobId);

    /// <summary>
    /// Cancels a running backup job.
    /// </summary>
    Task CancelJob(Guid jobId);

    /// <summary>
    /// Retries a failed backup job with exponential backoff.
    /// </summary>
    Task<BackupJob> RetryFailedJob(Guid jobId);
}
