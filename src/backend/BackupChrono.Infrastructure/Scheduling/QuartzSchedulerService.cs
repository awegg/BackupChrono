using System.Collections.Specialized;
using System.Threading;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;

namespace BackupChrono.Infrastructure.Scheduling;

/// <summary>
/// Service for managing scheduled backup jobs using Quartz.NET.
/// </summary>
public class QuartzSchedulerService : IQuartzSchedulerService
{
    internal IScheduler? Scheduler => _scheduler;
    private IScheduler? _scheduler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuartzSchedulerService> _logger;
    private readonly SemaphoreSlim _schedulerLock = new(1, 1);
    private readonly string _schedulerName;

    public QuartzSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<QuartzSchedulerService> logger,
        string? schedulerName = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _schedulerName = schedulerName ?? "DefaultQuartzScheduler";
    }

    /// <summary>
    /// Initializes the scheduler asynchronously.
    /// </summary>
    private async Task InitializeScheduler()
    {
        if (_scheduler != null)
        {
            return;
        }

        await _schedulerLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_scheduler == null)
            {
                // Create scheduler with unique name to avoid conflicts in tests
                var properties = new NameValueCollection
                {
                    ["quartz.scheduler.instanceName"] = _schedulerName
                };
                var schedulerFactory = new StdSchedulerFactory(properties);
                _scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
                
                // Use a job factory that supports dependency injection
                _scheduler.JobFactory = new MicrosoftDependencyInjectionJobFactory(_scopeFactory);
            }
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    /// <summary>
    /// Gets the scheduler, ensuring it's initialized.
    /// </summary>
    private async Task<IScheduler> GetSchedulerAsync()
    {
        await InitializeScheduler();
        return _scheduler ?? throw new InvalidOperationException("Scheduler failed to initialize");
    }

    /// <summary>
    /// Starts the scheduler and loads all device/share schedules.
    /// </summary>
    public async Task Start()
    {
        _logger.LogInformation("Starting Quartz scheduler...");
        
        await InitializeScheduler();
        if (_scheduler == null)
        {
            throw new InvalidOperationException("Failed to initialize scheduler");
        }

        await _scheduler.Start();

        // Schedule all devices and shares
        await ScheduleAllBackups();

        _logger.LogInformation("Quartz scheduler started successfully");
    }

    /// <summary>
    /// Stops the scheduler.
    /// </summary>
    public async Task Stop()
    {
        if (_scheduler == null)
        {
            _logger.LogInformation("Scheduler not initialized, nothing to stop");
            return;
        }

        _logger.LogInformation("Stopping Quartz scheduler...");
        await _scheduler.Shutdown(waitForJobsToComplete: true);
        _logger.LogInformation("Quartz scheduler stopped");
    }

    /// <summary>
    /// Schedules backups for all devices and shares based on their configurations.
    /// </summary>
    public async Task ScheduleAllBackups()
    {
        _logger.LogInformation("Scheduling all backups...");
        var scheduler = await GetSchedulerAsync();

        using var scope = _scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var shareService = scope.ServiceProvider.GetRequiredService<IShareService>();

        var devices = await deviceService.ListDevices();

        foreach (var device in devices)
        {
            try
            {
                // Get all shares for this device
                var shares = await shareService.ListShares(device.Id);

                // Check if any share has its own schedule
                var sharesWithSchedule = shares.Where(s => s.Enabled && s.Schedule != null).ToList();
                var sharesWithoutSchedule = shares.Where(s => s.Enabled && s.Schedule == null).ToList();

                // Schedule share-level backups for shares with their own schedules
                foreach (var share in sharesWithSchedule)
                {
                    try
                    {
                        // Normalize cron expression: convert * * to * ? to avoid Quartz errors
                        var normalizedCron = NormalizeCronExpression(share.Schedule!.CronExpression);
                        share.Schedule = new Schedule 
                        { 
                            CronExpression = normalizedCron,
                            TimeWindowStart = share.Schedule.TimeWindowStart,
                            TimeWindowEnd = share.Schedule.TimeWindowEnd
                        };
                        await ScheduleShareBackup(device, share, share.Schedule);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to schedule share {ShareId} for device {DeviceId}", share.Id, device.Id);
                    }
                }

                // If device has a schedule and there are shares without individual schedules,
                // schedule a device-level backup
                if (device.Schedule != null && sharesWithoutSchedule.Any())
                {
                    try
                    {
                        var normalizedCron = NormalizeCronExpression(device.Schedule.CronExpression);
                        device.Schedule = new Schedule
                        {
                            CronExpression = normalizedCron,
                            TimeWindowStart = device.Schedule.TimeWindowStart,
                            TimeWindowEnd = device.Schedule.TimeWindowEnd
                        };
                        await ScheduleDeviceBackup(device, device.Schedule);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to schedule device {DeviceId}", device.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error scheduling backups for device {DeviceId}", device.Id);
            }
        }

        _logger.LogInformation("All backups scheduled");
    }

    /// <summary>
    /// Schedules a device-level backup job.
    /// </summary>
    public async Task ScheduleDeviceBackup(Device device, Core.ValueObjects.Schedule schedule)
    {
        var scheduler = await GetSchedulerAsync();
        var jobKey = new JobKey($"device-{device.Id}", "backups");
        var triggerKey = new TriggerKey($"device-{device.Id}-trigger", "backups");

        // Remove existing job if any
        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
        }

        // Create job
        var job = JobBuilder.Create<BackupJob>()
            .WithIdentity(jobKey)
            .UsingJobData("DeviceId", device.Id.ToString())
            .UsingJobData("DeviceName", device.Name)
            .UsingJobData("JobType", "Scheduled")
            .Build();

        // Create trigger with cron schedule
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(schedule.CronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation(
            "Scheduled device-level backup for '{DeviceName}' with cron: {Cron}",
            device.Name,
            schedule.CronExpression);
    }

    /// <summary>
    /// Schedules a share-level backup job.
    /// </summary>
    public async Task ScheduleShareBackup(Device device, Share share, Core.ValueObjects.Schedule schedule)
    {
        var scheduler = await GetSchedulerAsync();
        var jobKey = new JobKey($"share-{share.Id}", "backups");
        var triggerKey = new TriggerKey($"share-{share.Id}-trigger", "backups");

        // Remove existing job if any
        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
        }

        // Create job
        var job = JobBuilder.Create<BackupJob>()
            .WithIdentity(jobKey)
            .UsingJobData("DeviceId", device.Id.ToString())
            .UsingJobData("ShareId", share.Id.ToString())
            .UsingJobData("DeviceName", device.Name)
            .UsingJobData("ShareName", share.Name)
            .UsingJobData("JobType", "Scheduled")
            .Build();

        // Create trigger with cron schedule
        if (string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            throw new ArgumentException("Cron expression cannot be null or empty", nameof(schedule));
        }
        
        _logger.LogInformation("Scheduling share with cron expression: '{CronExpression}'", schedule.CronExpression);
        
        // Validate cron expression before scheduling
        try
        {
            var cronExpr = new Quartz.CronExpression(schedule.CronExpression);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning("Invalid cron expression '{CronExpression}': {Message}", schedule.CronExpression, ex.Message);
            throw new ArgumentException($"Invalid cron expression: {ex.Message}", nameof(schedule), ex);
        }
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .WithCronSchedule(schedule.CronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation(
            "Scheduled share-level backup for '{DeviceName}/{ShareName}' with cron: {Cron}",
            device.Name,
            share.Name,
            schedule.CronExpression);
    }

    /// <summary>
    /// Unschedules a device backup job.
    /// </summary>
    public async Task UnscheduleDeviceBackup(Guid deviceId)
    {
        var scheduler = await GetSchedulerAsync();
        var jobKey = new JobKey($"device-{deviceId}", "backups");
        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
            _logger.LogInformation("Unscheduled device backup for device ID: {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Unschedules a share backup job.
    /// </summary>
    public async Task UnscheduleShareBackup(Guid shareId)
    {
        var scheduler = await GetSchedulerAsync();
        var jobKey = new JobKey($"share-{shareId}", "backups");
        if (await scheduler.CheckExists(jobKey))
        {
            await scheduler.DeleteJob(jobKey);
            _logger.LogInformation("Unscheduled share backup for share ID: {ShareId}", shareId);
        }
    }

    /// <summary>
    /// Triggers an immediate backup job (manual trigger).
    /// </summary>
    public async Task TriggerImmediateBackup(Guid deviceId, Guid? shareId = null)
    {
        var scheduler = await GetSchedulerAsync();
        var jobKey = shareId.HasValue
            ? new JobKey($"manual-share-{shareId.Value}", "manual-backups")
            : new JobKey($"manual-device-{deviceId}", "manual-backups");

        var jobData = new JobDataMap
        {
            { "DeviceId", deviceId.ToString() },
            { "JobType", "Manual" }
        };

        if (shareId.HasValue)
        {
            jobData["ShareId"] = shareId.Value.ToString();
        }

        var job = JobBuilder.Create<BackupJob>()
            .WithIdentity(jobKey)
            .SetJobData(jobData)
            // Non-durable: job will be removed when trigger completes
            .Build();

        // Create an immediate trigger
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobKey.Name}_trigger", jobKey.Group)
            .ForJob(jobKey)
            .StartNow()
            .WithSimpleSchedule(x => x.WithMisfireHandlingInstructionFireNow())
            .Build();

        // Schedule job with trigger atomically (no need for AddJob + TriggerJob)
        // If job already exists, replace it
        await scheduler.ScheduleJob(job, new[] { trigger }, replace: true);

        _logger.LogInformation(
            "Triggered immediate backup for device {DeviceId}, share {ShareId}",
            deviceId,
            shareId?.ToString() ?? "all");
    }

    public async Task CancelJob(Guid jobId)
    {
        // Delegate to BackupOrchestrator which manages the actual job execution and cancellation
        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBackupOrchestrator>();
        await orchestrator.CancelJob(jobId);
    }

    /// <summary>
    /// Normalize cron expression to fix Quartz compatibility issues.
    /// Converts '* *' (both day-of-month and day-of-week as wildcards) to '* ?' 
    /// since Quartz requires one to be '?'.
    /// </summary>
    private string NormalizeCronExpression(string cron)
    {
        if (string.IsNullOrWhiteSpace(cron)) return cron;
        
        var parts = cron.Split(' ');
        if (parts.Length < 6) return cron;

        // Check if day-of-month (index 3) and day-of-week (index 5) are both '*'
        if (parts[3] == "*" && parts[5] == "*")
        {
            parts[5] = "?"; // Convert day-of-week to '?'
            _logger.LogWarning("Normalized cron expression from '{Original}' to '{Normalized}'", cron, string.Join(" ", parts));
            return string.Join(" ", parts);
        }

        return cron;
    }
}
