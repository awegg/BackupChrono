namespace BackupChrono.Core.DTOs;

/// <summary>
/// Progress information for a running restore operation.
/// </summary>
public class RestoreProgress
{
    /// <summary>
    /// Unique identifier for the restore operation.
    /// </summary>
    public required string RestoreId { get; init; }

    /// <summary>
    /// Number of files restored so far.
    /// </summary>
    public int FilesRestored { get; init; }

    /// <summary>
    /// Number of bytes restored so far.
    /// </summary>
    public long BytesRestored { get; init; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double PercentComplete { get; init; }
}
