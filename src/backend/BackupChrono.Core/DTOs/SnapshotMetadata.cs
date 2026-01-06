namespace BackupChrono.Core.DTOs;

/// <summary>
/// Detailed snapshot metadata from restic.
/// </summary>
public class SnapshotMetadata
{
    /// <summary>
    /// Snapshot ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Parent snapshot ID (null for first backup).
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Hostname of the backed-up device.
    /// </summary>
    public required string Hostname { get; set; }

    /// <summary>
    /// Paths included in this snapshot.
    /// </summary>
    public required List<string> Paths { get; set; }

    /// <summary>
    /// Snapshot creation timestamp.
    /// </summary>
    public required DateTime Time { get; set; }

    /// <summary>
    /// Tags associated with the snapshot.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Summary statistics from the backup.
    /// </summary>
    public SnapshotSummary? Summary { get; set; }
}

/// <summary>
/// Summary statistics from a backup snapshot.
/// </summary>
public class SnapshotSummary
{
    /// <summary>
    /// Number of new files.
    /// </summary>
    public int FilesNew { get; set; }

    /// <summary>
    /// Number of changed files.
    /// </summary>
    public int FilesChanged { get; set; }

    /// <summary>
    /// Number of unmodified files.
    /// </summary>
    public int FilesUnmodified { get; set; }

    /// <summary>
    /// Number of new directories.
    /// </summary>
    public int DirsNew { get; set; }

    /// <summary>
    /// Number of changed directories.
    /// </summary>
    public int DirsChanged { get; set; }

    /// <summary>
    /// Number of unmodified directories.
    /// </summary>
    public int DirsUnmodified { get; set; }

    /// <summary>
    /// Total bytes added in this backup.
    /// </summary>
    public long DataAdded { get; set; }

    /// <summary>
    /// Total bytes processed before deduplication (raw data scanned).
    /// </summary>
    public long DataProcessed { get; set; }

    /// <summary>
    /// Total files processed.
    /// </summary>
    public int TotalFilesProcessed { get; set; }

    /// <summary>
    /// Total bytes processed (sum of all file sizes scanned, may differ from DataProcessed due to compression/metadata).
    /// </summary>
    public long TotalBytesProcessed { get; set; }
}
