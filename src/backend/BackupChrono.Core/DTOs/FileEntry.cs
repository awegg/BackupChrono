namespace BackupChrono.Core.DTOs;

/// <summary>
/// Represents a file or directory entry in a backup snapshot.
/// </summary>
public class FileEntry
{
    /// <summary>
    /// File or directory name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path within the backup.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether this entry is a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// File size in bytes (0 for directories).
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; init; }

    /// <summary>
    /// Unix-style permissions string (e.g., "rwxr-xr-x").
    /// </summary>
    public string? Permissions { get; init; }
}
