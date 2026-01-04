namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents a backup snapshot at a specific point in time.
/// Can be device-level (all shares) or share-level (single share).
/// </summary>
public class Backup
{
    /// <summary>
    /// Unique backup identifier from restic (e.g., "a1b2c3d4").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Foreign key to the device that was backed up.
    /// </summary>
    public required Guid DeviceId { get; set; }

    /// <summary>
    /// Foreign key to the share (null for device-level backups).
    /// </summary>
    public Guid? ShareId { get; set; }

    /// <summary>
    /// Device name (denormalized for query performance).
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Share name (denormalized, null for device-level backups).
    /// </summary>
    public string? ShareName { get; set; }

    /// <summary>
    /// Timestamp when the backup was created (UTC).
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Backup status (Success, Partial, Failed).
    /// </summary>
    public required BackupStatus Status { get; init; }

    /// <summary>
    /// Share names to paths included in this backup.
    /// For device-level: contains all shares. For share-level: contains single entry.
    /// </summary>
    public Dictionary<string, string> SharesPaths { get; init; } = new();

    /// <summary>
    /// Number of new files in this backup.
    /// </summary>
    public int FilesNew { get; init; }

    /// <summary>
    /// Number of changed files.
    /// </summary>
    public int FilesChanged { get; init; }

    /// <summary>
    /// Number of unchanged files.
    /// </summary>
    public int FilesUnmodified { get; init; }

    /// <summary>
    /// Bytes of new data added.
    /// </summary>
    public long DataAdded { get; init; }

    /// <summary>
    /// Total bytes processed.
    /// </summary>
    public long DataProcessed { get; init; }

    /// <summary>
    /// Backup duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error details if status is Failed or Partial.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Backup job that created this backup (if applicable).
    /// </summary>
    public Guid? CreatedByJobId { get; init; }
}
