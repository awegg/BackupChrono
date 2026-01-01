namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Service for monitoring storage capacity and alerting on thresholds.
/// </summary>
public interface IStorageMonitor
{
    /// <summary>
    /// Gets the current storage status for a given path.
    /// </summary>
    Task<StorageStatus> GetStorageStatus(string path);

    /// <summary>
    /// Checks if there is sufficient storage available for a backup operation.
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <param name="estimatedSizeBytes">Estimated size needed in bytes</param>
    Task<bool> HasSufficientSpace(string path, long estimatedSizeBytes);

    /// <summary>
    /// Gets storage status for all repository paths.
    /// </summary>
    Task<IEnumerable<StorageStatus>> GetAllRepositoryStorageStatus();
}

/// <summary>
/// Storage status information.
/// </summary>
public class StorageStatus
{
    public required string Path { get; set; }
    public long TotalBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercentage { get; set; }
    public StorageThresholdLevel ThresholdLevel { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Storage threshold levels based on configurable thresholds (see StorageMonitorOptions).
/// Default: Warning at 80%, Critical at 90%, Exhausted at 95%.
/// </summary>
public enum StorageThresholdLevel
{
    /// <summary>Storage utilization is normal (below warning threshold).</summary>
    Normal,

    /// <summary>Storage utilization has exceeded warning threshold (default 80%).</summary>
    Warning,

    /// <summary>Storage utilization has exceeded critical threshold (default 90%).</summary>
    Critical,

    /// <summary>Storage utilization has exceeded exhausted threshold (default 95%).</summary>
    Exhausted
}
