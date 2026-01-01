namespace BackupChrono.Core.Entities;

/// <summary>
/// Status of a backup job execution.
/// </summary>
public enum BackupJobStatus
{
    /// <summary>
    /// Job is waiting to start.
    /// </summary>
    Pending,

    /// <summary>
    /// Job is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed with errors.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled by user.
    /// </summary>
    Cancelled,
    /// <summary>
    /// Job completed with partial success (some operations succeeded, others failed).
    /// </summary>
    PartiallyCompleted}
