namespace BackupChrono.Core.Entities;

/// <summary>
/// Status of a backup snapshot.
/// </summary>
public enum BackupStatus
{
    /// <summary>
    /// Backup completed successfully with all files backed up.
    /// </summary>
    Success,

    /// <summary>
    /// Backup completed but some files were skipped or had errors.
    /// </summary>
    Partial,

    /// <summary>
    /// Backup failed and was not completed.
    /// </summary>
    Failed
}
