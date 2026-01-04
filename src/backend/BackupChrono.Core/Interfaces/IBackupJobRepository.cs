using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Repository for persisting and querying backup job entities.
/// </summary>
public interface IBackupJobRepository
{
    /// <summary>
    /// Saves a backup job. Creates a new job or updates an existing one.
    /// </summary>
    /// <param name="job">The backup job to save.</param>
    /// <returns>The saved backup job.</returns>
    Task<BackupJob> SaveJob(BackupJob job);
    
    /// <summary>
    /// Retrieves a backup job by its ID.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <returns>The backup job if found; otherwise, null.</returns>
    Task<BackupJob?> GetJob(Guid jobId);
    
    /// <summary>
    /// Lists all backup jobs.
    /// </summary>
    /// <returns>A list of all backup jobs.</returns>
    Task<List<BackupJob>> ListJobs();
    
    /// <summary>
    /// Lists all backup jobs for a specific device.
    /// </summary>
    /// <param name="deviceId">The device ID to filter by.</param>
    /// <returns>A list of backup jobs for the specified device.</returns>
    Task<List<BackupJob>> ListJobsByDevice(Guid deviceId);
    
    /// <summary>
    /// Lists all backup jobs with a specific status.
    /// </summary>
    /// <param name="status">The job status to filter by.</param>
    /// <returns>A list of backup jobs with the specified status.</returns>
    Task<List<BackupJob>> ListJobsByStatus(BackupJobStatus status);
    
    /// <summary>
    /// Deletes a backup job by its ID.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to delete.</param>
    /// <returns>True if the job was deleted; false if the job was not found.</returns>
    Task<bool> DeleteJob(Guid jobId);
}