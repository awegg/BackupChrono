namespace BackupChrono.Core.DTOs;

/// <summary>
/// Repository statistics for a specific snapshot (deduplication metrics).
/// </summary>
public class SnapshotStats
{
    /// <summary>
    /// Snapshot ID these stats apply to.
    /// </summary>
    public required string SnapshotId { get; set; }

    /// <summary>
    /// Total number of data blobs.
    /// </summary>
    public long TotalBlobCount { get; set; }

    /// <summary>
    /// Total number of tree blobs.
    /// </summary>
    public long TotalTreeCount { get; set; }

    /// <summary>
    /// Total size in bytes (after deduplication).
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Total uncompressed size in bytes (original data size).
    /// </summary>
    public long TotalUncompressedSize { get; set; }

    /// <summary>
    /// Compression ratio as percentage (e.g., 0.75 = 75% compression).
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Space saved by deduplication in bytes.
    /// </summary>
    public long DeduplicationSpaceSaved { get; set; }

    /// <summary>
    /// Deduplication ratio as percentage (e.g., 0.30 = 30% deduplicated).
    /// </summary>
    public double DeduplicationRatio { get; set; }
}
