using BackupChrono.Core.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Main service for interacting with the restic backup engine.
/// </summary>
public interface IResticService
{
    // Repository management

    /// <summary>
    /// Initializes a new restic repository.
    /// </summary>
    Task<bool> InitializeRepository(string repositoryPath, string password);

    /// <summary>
    /// Checks if a repository exists and is valid.
    /// </summary>
    Task<bool> RepositoryExists(string repositoryPath);

    /// <summary>
    /// Gets repository statistics (size, deduplication savings, etc.).
    /// </summary>
    Task<RepositoryStats> GetStats(string repositoryPath);

    /// <summary>
    /// Verifies repository integrity.
    /// </summary>
    Task VerifyIntegrity(string repositoryPath);

    /// <summary>
    /// Prunes unused data blocks from the repository.
    /// </summary>
    Task Prune(string repositoryPath);

    // Backup operations

    /// <summary>
    /// Creates a new backup.
    /// </summary>
    /// <param name="repositoryPath">Path to the restic repository.</param>
    /// <param name="device">Device being backed up.</param>
    /// <param name="share">Share to backup (null for device-level backup of all shares).</param>
    /// <param name="sourcePath">Local path to backup (from mounted share).</param>
    /// <param name="rules">Include/exclude rules to apply.</param>
    /// <param name="onProgress">Optional callback for progress updates during backup.</param>
    /// <returns>Created backup snapshot.</returns>
    Task<Backup> CreateBackup(string repositoryPath, Device device, Share? share, string sourcePath, IncludeExcludeRules rules, Action<BackupProgress>? onProgress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets progress of a running backup job.
    /// </summary>
    Task<BackupProgress> GetBackupProgress(string jobId);

    /// <summary>
    /// Cancels a running backup job.
    /// </summary>
    Task CancelBackup(string jobId);

    // Backup management

    /// <summary>
    /// Lists all backups, optionally filtered by device name.
    /// </summary>
    Task<IEnumerable<Backup>> ListBackups(string? deviceName = null);

    /// <summary>
    /// Gets a specific backup by ID.
    /// </summary>
    Task<Backup> GetBackup(string backupId);

    /// <summary>
    /// Browses files in a backup snapshot.
    /// </summary>
    Task<IEnumerable<FileEntry>> BrowseBackup(string backupId, string path = "/");

    /// <summary>
    /// Gets version history for a specific file across backups.
    /// </summary>
    Task<IEnumerable<FileVersion>> GetFileHistory(string deviceName, string filePath);

    // Retention

    /// <summary>
    /// Applies retention policy to remove old backups.
    /// </summary>
    Task ApplyRetentionPolicy(string deviceName, RetentionPolicy policy);

    // Restore

    /// <summary>
    /// Restores files from a backup.
    /// </summary>
    /// <param name="backupId">Backup snapshot to restore from.</param>
    /// <param name="targetPath">Local path to restore to.</param>
    /// <param name="includePaths">Optional specific paths to restore (null = restore all).</param>
    Task RestoreBackup(string backupId, string targetPath, string[]? includePaths = null);

    /// <summary>
    /// Gets progress of a running restore operation.
    /// </summary>
    Task<RestoreProgress> GetRestoreProgress(string restoreId);
}
