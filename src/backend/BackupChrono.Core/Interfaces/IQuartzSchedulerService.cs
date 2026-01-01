using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Schedules and manages backup jobs using Quartz.
/// </summary>
public interface IQuartzSchedulerService
{
    /// <summary>
    /// Starts the scheduler and registers all jobs.
    /// </summary>
    /// <returns>Task that completes when the scheduler is running.</returns>
    Task Start();

    /// <summary>
    /// Stops the scheduler and prevents new jobs from starting.
    /// </summary>
    /// <returns>Task that completes when the scheduler is stopped.</returns>
    Task Stop();

    /// <summary>
    /// Schedules all configured backups for all devices and shares.
    /// </summary>
    /// <returns>Task that completes after all schedules are registered.</returns>
    Task ScheduleAllBackups();

    /// <summary>
    /// Schedules recurring backups for a device using the provided schedule.
    /// </summary>
    /// <param name="device">Device to schedule.</param>
    /// <param name="schedule">Schedule definition to apply.</param>
    /// <returns>Task that completes after the device jobs are registered.</returns>
    Task ScheduleDeviceBackup(Device device, Schedule schedule);

    /// <summary>
    /// Schedules recurring backups for a specific share using the provided schedule.
    /// </summary>
    /// <param name="device">Parent device that owns the share.</param>
    /// <param name="share">Share to schedule.</param>
    /// <param name="schedule">Schedule definition to apply.</param>
    /// <returns>Task that completes after the share job is registered.</returns>
    Task ScheduleShareBackup(Device device, Share share, Schedule schedule);

    /// <summary>
    /// Triggers an immediate backup for a device or a specific share on that device.
    /// </summary>
    /// <param name="deviceId">Device identifier to run.</param>
    /// <param name="shareId">Optional share identifier; if null, run the device-level backup.</param>
    /// <returns>Task that completes when the trigger request is dispatched.</returns>
    Task TriggerImmediateBackup(Guid deviceId, Guid? shareId = null);
}
