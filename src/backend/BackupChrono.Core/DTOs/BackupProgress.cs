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
    /// Number of files processed so far.
    /// </summary>
    public int FilesProcessed { get; init; }

    /// <summary>
    /// Number of bytes transferred so far.
    /// </summary>
    public long BytesTransferred { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete { get; init; }

    /// <summary>
    /// Path of the file currently being processed.
    /// </summary>
    public string? CurrentFile { get; init; }
}
