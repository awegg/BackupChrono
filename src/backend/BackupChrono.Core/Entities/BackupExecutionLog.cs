namespace BackupChrono.Core.Entities;

/// <summary>
/// Stores execution logs for a backup job (warnings, errors, progress).
/// </summary>
public class BackupExecutionLog
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Backup ID this log is associated with.
    /// </summary>
    public required string BackupId { get; set; }

    /// <summary>
    /// Backup job ID that created this backup.
    /// </summary>
    public string? JobId { get; set; }

    /// <summary>
    /// Warning messages from backup execution.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Error messages from backup execution.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Progress log entries from backup execution.
    /// </summary>
    public List<ProgressLogEntry> ProgressLog { get; set; } = new();

    /// <summary>
    /// When this log was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this log was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single entry in backup progress log.
/// </summary>
public class ProgressLogEntry
{
    /// <summary>
    /// Timestamp of this log entry.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Log message (e.g., "Scanning directory", "Processing file").
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Backup progress percentage (0-100).
    /// </summary>
    public int PercentDone { get; set; }

    /// <summary>
    /// Current file being processed.
    /// </summary>
    public string? CurrentFile { get; set; }

    /// <summary>
    /// Number of files processed so far.
    /// </summary>
    public long FilesDone { get; set; }

    /// <summary>
    /// Bytes processed so far.
    /// </summary>
    public long BytesDone { get; set; }
}
