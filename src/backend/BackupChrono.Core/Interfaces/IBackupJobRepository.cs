using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

public interface IBackupJobRepository
{
    Task<BackupJob> SaveJob(BackupJob job);
    Task<BackupJob?> GetJob(Guid jobId);
    Task<List<BackupJob>> ListJobs();
    Task<List<BackupJob>> ListJobsByDevice(Guid deviceId);
    Task<List<BackupJob>> ListJobsByStatus(BackupJobStatus status);
}
