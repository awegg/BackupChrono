using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Api.Services;

/// <summary>
/// Background service that cleans up stale "Running" jobs on application startup.
/// Jobs that were running when the app crashed or was terminated are marked as Failed.
/// </summary>
public class JobCleanupService : IHostedService
{
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly ILogger<JobCleanupService> _logger;

    public JobCleanupService(
        IBackupJobRepository backupJobRepository,
        ILogger<JobCleanupService> logger)
    {
        _backupJobRepository = backupJobRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting job cleanup service - checking for stale running jobs");

        try
        {
            var allJobs = await _backupJobRepository.ListJobs();
            var staleJobs = allJobs.Where(j => j.Status == BackupJobStatus.Running).ToList();

            if (staleJobs.Any())
            {
                _logger.LogWarning("Found {Count} stale 'Running' jobs from previous session - marking as Failed", staleJobs.Count);

                foreach (var job in staleJobs)
                {
                    _logger.LogInformation("Marking job {JobId} ({DeviceName}/{ShareName}) as Failed", 
                        job.Id, job.DeviceName, job.ShareName ?? "all shares");

                    job.Status = BackupJobStatus.Failed;
                    job.ErrorMessage = "Backup interrupted - application was stopped while backup was running";
                    job.CompletedAt = DateTime.UtcNow;

                    await _backupJobRepository.SaveJob(job);
                }

                _logger.LogInformation("Successfully cleaned up {Count} stale jobs", staleJobs.Count);
            }
            else
            {
                _logger.LogInformation("No stale running jobs found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during job cleanup");
            // Don't throw - we don't want to prevent app startup if cleanup fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nothing to do on shutdown
        return Task.CompletedTask;
    }
}
