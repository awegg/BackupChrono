namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents a scheduled or on-demand execution of a backup operation.
/// </summary>
public class BackupJob
{
    /// <summary>
    /// Unique identifier for the backup job.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the device being backed up.
    /// </summary>
    public required Guid DeviceId { get; set; }

    /// <summary>
    /// Foreign key to the share (null if device-level backup).
    /// </summary>
    public Guid? ShareId { get; set; }

    /// <summary>
    /// Device name (for display purposes).
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Share name (for display purposes, null if device-level backup).
    /// </summary>
    public string? ShareName { get; set; }

    /// <summary>
    /// Type of backup job (Scheduled, Manual, Retry).
    /// </summary>
    public required BackupJobType Type { get; set; }

    /// <summary>
    /// Current job status (Pending, Running, Completed, Failed, Cancelled).
    /// </summary>
    public required BackupJobStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the job started (UTC).
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when the job completed (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Backup identifier if the job was successful.
    /// </summary>
    public string? BackupId { get; set; }

    /// <summary>
    /// Number of files processed during the backup.
    /// </summary>
    public int FilesProcessed { get; set; }

    /// <summary>
    /// Number of bytes transferred during the backup.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts (for exponential backoff tracking).
    /// </summary>
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Scheduled time for the next retry (if applicable).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// The restic command line executed for this backup (for debugging).
    /// </summary>
    public string? CommandLine { get; set; }
}
