namespace BackupChrono.Core.Entities;

/// <summary>
/// Represents a deduplicated chunk of file data (managed by restic).
/// This is a conceptual entity - actual management is handled by the restic backup engine.
/// </summary>
public class DataBlock
{
    /// <summary>
    /// SHA-256 hash of the data block (primary key).
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Size of the data block in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Number of backups referencing this block.
    /// </summary>
    public int RefCount { get; set; }
}
