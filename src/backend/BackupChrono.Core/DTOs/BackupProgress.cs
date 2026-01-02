namespace BackupChrono.Core.DTOs;

/// <summary>
/// Progress information for a running backup operation.
/// </summary>
public class BackupProgress
{
    /// <summary>
    /// Unique identifier for the backup job.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Device name being backed up.
    /// </summary>
    public string? DeviceName { get; init; }

    /// <summary>
    /// Share name being backed up.
    /// </summary>
    public string? ShareName { get; init; }

    /// <summary>
    /// Current status of the backup (Running, Completed, Failed).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Number of files processed so far.
    /// </summary>
    public int FilesProcessed { get; init; }

    /// <summary>
    /// Total number of files to process (if known).
    /// </summary>
    public int? TotalFiles { get; init; }

    /// <summary>
    /// Number of bytes transferred so far.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Total bytes to transfer (if known).
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete { get; init; }

    /// <summary>
    /// Path of the file currently being processed.
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Error message if the backup failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
