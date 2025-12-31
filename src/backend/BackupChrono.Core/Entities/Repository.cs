namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents the restic backup repository containing all backup data.
/// </summary>
public class Repository
{
    /// <summary>
    /// Unique identifier for the repository.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Path to the repository (local path or remote URL).
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Whether the repository has been initialized.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Total size of the repository in bytes.
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Number of snapshots in the repository.
    /// </summary>
    public int SnapshotCount { get; set; }

    /// <summary>
    /// Timestamp of the last repository check.
    /// </summary>
    public DateTime? LastCheckAt { get; set; }

    /// <summary>
    /// Timestamp when the repository was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the repository was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public Repository()
    {
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
