using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Interfaces;

public interface IQuartzSchedulerService
{
    Task Start();
    Task Stop();
    Task ScheduleAllBackups();
    Task ScheduleDeviceBackup(Device device, Schedule schedule);
    Task ScheduleShareBackup(Device device, Share share, Schedule schedule);
    Task TriggerImmediateBackup(Guid deviceId, Guid? shareId = null);
}
