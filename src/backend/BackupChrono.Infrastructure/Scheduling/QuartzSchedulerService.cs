using System.Threading;
using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
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
    private IScheduler? _scheduler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuartzSchedulerService> _logger;
    private readonly SemaphoreSlim _schedulerLock = new(1, 1);

    public QuartzSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<QuartzSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
                var schedulerFactory = new StdSchedulerFactory();
                _scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
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
            // Get all shares for this device
            var shares = await shareService.ListShares(device.Id);

            // Check if any share has its own schedule
            var sharesWithSchedule = shares.Where(s => s.Enabled && s.Schedule != null).ToList();
            var sharesWithoutSchedule = shares.Where(s => s.Enabled && s.Schedule == null).ToList();

            // Schedule share-level backups for shares with their own schedules
            foreach (var share in sharesWithSchedule)
            {
                await ScheduleShareBackup(device, share, share.Schedule!);
            }

            // If device has a schedule and there are shares without individual schedules,
            // schedule a device-level backup
            if (device.Schedule != null && sharesWithoutSchedule.Any())
            {
                await ScheduleDeviceBackup(device, device.Schedule);
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
            .Build();

        // Ensure the job exists in the scheduler before triggering
        await scheduler.AddJob(job, replace: true);

        await scheduler.TriggerJob(jobKey, jobData);

        _logger.LogInformation(
            "Triggered immediate backup for device {DeviceId}, share {ShareId}",
            deviceId,
            shareId?.ToString() ?? "all");
    }
}
