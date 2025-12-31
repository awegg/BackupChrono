namespace BackupChrono.Core.Entities;

/// <summary>
/// Type of backup job execution.
/// </summary>
public enum BackupJobType
{
    /// <summary>
    /// Scheduled backup triggered by timer.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Manual backup triggered via API.
    /// </summary>
    Manual,

    /// <summary>
    /// Retry of a previously failed backup.
    /// </summary>
    Retry
}
