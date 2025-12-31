namespace BackupChrono.Core.DTOs;

/// <summary>
/// Represents a specific version of a file across backup snapshots.
/// </summary>
public class FileVersion
{
    /// <summary>
    /// Backup snapshot ID containing this file version.
    /// </summary>
    public required string BackupId { get; init; }

    /// <summary>
    /// Timestamp when this version was backed up.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Content hash (for detecting identical files).
    /// </summary>
    public required string Hash { get; init; }
}
