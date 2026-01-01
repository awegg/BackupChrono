using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace BackupChrono.Infrastructure.Scheduling;

/// <summary>
/// Quartz job that executes backup operations via BackupOrchestrator.
/// </summary>
[DisallowConcurrentExecution]
public class BackupJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackupJob> _logger;

    public BackupJob(IServiceProvider serviceProvider, ILogger<BackupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;

        // Extract job parameters
        var deviceIdStr = dataMap.GetString("DeviceId");
        var shareIdStr = dataMap.GetString("ShareId");
        var jobTypeStr = dataMap.GetString("JobType") ?? "Scheduled";
        var deviceName = dataMap.GetString("DeviceName");
        var shareName = dataMap.GetString("ShareName");
        var retryAttemptStr = dataMap.GetString("RetryAttempt");

        if (string.IsNullOrEmpty(deviceIdStr) || !Guid.TryParse(deviceIdStr, out var deviceId))
        {
            _logger.LogError("Invalid or missing DeviceId in job data");
            return;
        }

        if (!Enum.TryParse<BackupJobType>(jobTypeStr, out var jobType))
        {
            _logger.LogWarning("Invalid JobType '{JobType}', defaulting to Scheduled", jobTypeStr);
            jobType = BackupJobType.Scheduled;
        }

        int retryAttempt = 0;
        if (!string.IsNullOrEmpty(retryAttemptStr) && int.TryParse(retryAttemptStr, out var parsedRetryAttempt))
        {
            retryAttempt = parsedRetryAttempt;
        }

        Guid? shareId = null;
        if (!string.IsNullOrEmpty(shareIdStr) && Guid.TryParse(shareIdStr, out var parsedShareId))
        {
            shareId = parsedShareId;
        }

        // Create a scope for dependency injection
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBackupOrchestrator>();

        try
        {
            Core.Entities.BackupJob backupJob;

            if (shareId.HasValue)
            {
                // Share-level backup
                _logger.LogInformation(
                    "Executing scheduled share backup: {DeviceName}/{ShareName}",
                    deviceName ?? deviceId.ToString(),
                    shareName ?? shareId.ToString());

                backupJob = await orchestrator.ExecuteShareBackup(deviceId, shareId.Value, jobType);
            }
            else
            {
                // Device-level backup
                _logger.LogInformation(
                    "Executing scheduled device backup: {DeviceName}",
                    deviceName ?? deviceId.ToString());

                backupJob = await orchestrator.ExecuteDeviceBackup(deviceId, jobType);
            }

            // Set retry attempt on the backup job if this is a retry
            if (retryAttempt > 0)
            {
                backupJob.RetryAttempt = retryAttempt;
            }

            // Log result
            if (backupJob.Status == BackupJobStatus.Completed)
            {
                _logger.LogInformation(
                    "Backup job {JobId} completed successfully",
                    backupJob.Id);
            }
            else if (backupJob.Status == BackupJobStatus.Failed)
            {
                _logger.LogError(
                    "Backup job {JobId} failed: {Error}",
                    backupJob.Id,
                    backupJob.ErrorMessage);

                // Schedule retry with exponential backoff
                await ScheduleRetry(context, backupJob, deviceId, shareId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during backup job execution");
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }

    private async Task ScheduleRetry(
        IJobExecutionContext context,
        Core.Entities.BackupJob failedJob,
        Guid deviceId,
        Guid? shareId)
    {
        // Exponential backoff: 5min, 15min, 45min
        var retryDelays = new[] { 5, 15, 45 };

        if (failedJob.RetryAttempt >= retryDelays.Length)
        {
            _logger.LogWarning(
                "Maximum retry attempts ({MaxAttempts}) reached for job {JobId}, giving up",
                retryDelays.Length,
                failedJob.Id);
            return;
        }

        var delayMinutes = retryDelays[failedJob.RetryAttempt];
        var nextRetryTime = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);

        _logger.LogInformation(
            "Scheduling retry #{Attempt} for job {JobId} in {Delay} minutes (at {RetryTime})",
            failedJob.RetryAttempt + 1,
            failedJob.Id,
            delayMinutes,
            nextRetryTime);

        // Create retry job
        var retryJobKey = new JobKey($"retry-{failedJob.Id}", "retries");

        var retryJob = JobBuilder.Create<BackupJob>()
            .WithIdentity(retryJobKey)
            .UsingJobData("DeviceId", deviceId.ToString())
            .UsingJobData("JobType", "Retry")
            .UsingJobData("RetryAttempt", failedJob.RetryAttempt + 1)
            .Build();

        if (shareId.HasValue)
        {
            retryJob.JobDataMap.Add("ShareId", shareId.Value.ToString());
        }

        var retryTrigger = TriggerBuilder.Create()
            .WithIdentity($"retry-{failedJob.Id}-trigger", "retries")
            .StartAt(nextRetryTime)
            .Build();

        await context.Scheduler.ScheduleJob(retryJob, retryTrigger);
    }
}
