namespace BackupChrono.Core.DTOs;

/// <summary>
/// Repository statistics including size and deduplication information.
/// </summary>
public class RepositoryStats
{
    /// <summary>
    /// Total size of all backed up data (before deduplication).
    /// </summary>
    public long TotalSize { get; init; }

    /// <summary>
    /// Actual size on disk after deduplication.
    /// </summary>
    public long UniqueSize { get; init; }

    /// <summary>
    /// Bytes saved through deduplication.
    /// </summary>
    public long DeduplicationSavings => TotalSize - UniqueSize;

    /// <summary>
    /// Number of backup snapshots in the repository.
    /// </summary>
    public int BackupCount { get; init; }

    /// <summary>
    /// Total number of files across all backups.
    /// </summary>
    public int FileCount { get; init; }
}
