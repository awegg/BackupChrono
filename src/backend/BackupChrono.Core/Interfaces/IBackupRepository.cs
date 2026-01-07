using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Repository for querying and aggregating backup data.
/// Provides high-level queries for backup statistics and overview information.
/// </summary>
public interface IBackupRepository
{
    /// <summary>
    /// Gets the latest backup for a specific share.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="shareId">The share ID.</param>
    /// <returns>The most recent backup for the specified share, or null if no backup exists.</returns>
    Task<Backup?> GetLatestBackup(Guid deviceId, Guid shareId);

    /// <summary>
    /// Gets the latest backups for multiple shares in a single query.
    /// Optimized for batch operations to reduce API calls.
    /// </summary>
    /// <param name="shareIds">List of share IDs to query.</param>
    /// <returns>Dictionary mapping share ID to its latest backup (if it exists).</returns>
    Task<Dictionary<Guid, Backup>> GetLatestBackupsForShares(List<Guid> shareIds);

    /// <summary>
    /// Gets aggregate statistics across all backups.
    /// </summary>
    /// <returns>Overview statistics including totals, failures, and stale backups.</returns>
    Task<OverviewStatistics> GetOverviewStatistics();
}

/// <summary>
/// Aggregate statistics for backup overview.
/// </summary>
public class OverviewStatistics
{
    public int TotalDevices { get; set; }
    public int TotalShares { get; set; }
    public int DevicesWithFailures { get; set; }
    public int DevicesWithStaleBackups { get; set; }
    public long TotalProtectedBytes { get; set; }
    public int TotalFiles { get; set; }
}
