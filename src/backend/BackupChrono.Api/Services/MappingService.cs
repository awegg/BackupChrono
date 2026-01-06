using BackupChrono.Api.DTOs;
using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Api.Services;

public interface IMappingService
{
    DeviceDto ToDeviceDto(Device device);
    DeviceDetailDto ToDeviceDetailDto(Device device, List<Share> shares, Backup? lastBackup = null);
    Device ToDevice(DeviceCreateDto dto);
    void ApplyUpdate(Device device, DeviceUpdateDto dto);
    
    ShareDto ToShareDto(Share share);
    ShareDetailDto ToShareDetailDto(Share share, Backup? lastBackup = null);
    Share ToShare(Guid deviceId, ShareCreateDto dto);
    void ApplyUpdate(Share share, ShareUpdateDto dto);
    
    BackupJobDto ToBackupJobDto(BackupJob job);
    BackupDto ToBackupDto(Backup backup);
    
    ScheduleDto? ToScheduleDto(Schedule? schedule);
    Schedule? ToSchedule(ScheduleDto? dto);
    
    RetentionPolicyDto? ToRetentionPolicyDto(RetentionPolicy? policy);
    RetentionPolicy? ToRetentionPolicy(RetentionPolicyDto? dto);
    
    IncludeExcludeRulesDto? ToIncludeExcludeRulesDto(IncludeExcludeRules? rules);
    IncludeExcludeRules? ToIncludeExcludeRules(IncludeExcludeRulesDto? dto);
}

public class MappingService : IMappingService
{
    public DeviceDto ToDeviceDto(Device device)
    {
        return new DeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            Protocol = device.Protocol.ToString(),
            Host = device.Host,
            Port = device.Port,
            Username = device.Username,
            WakeOnLanEnabled = device.WakeOnLanEnabled,
            WakeOnLanMacAddress = device.WakeOnLanMacAddress,
            CreatedAt = device.CreatedAt,
            UpdatedAt = device.UpdatedAt
        };
    }

    public DeviceDetailDto ToDeviceDetailDto(Device device, List<Share> shares, Backup? lastBackup = null)
    {
        return new DeviceDetailDto
        {
            Id = device.Id,
            Name = device.Name,
            Protocol = device.Protocol.ToString(),
            Host = device.Host,
            Port = device.Port,
            Username = device.Username,
            WakeOnLanEnabled = device.WakeOnLanEnabled,
            WakeOnLanMacAddress = device.WakeOnLanMacAddress,
            CreatedAt = device.CreatedAt,
            UpdatedAt = device.UpdatedAt,
            Schedule = ToScheduleDto(device.Schedule),
            RetentionPolicy = ToRetentionPolicyDto(device.RetentionPolicy),
            IncludeExcludeRules = ToIncludeExcludeRulesDto(device.IncludeExcludeRules),
            Shares = shares.Select(ToShareDto).ToList(),
            LastBackup = lastBackup != null ? ToBackupDto(lastBackup) : null
        };
    }

    public Device ToDevice(DeviceCreateDto dto)
    {
        if (!Enum.TryParse<ProtocolType>(dto.Protocol, ignoreCase: true, out var protocol))
        {
            throw new ArgumentException($"Invalid protocol '{dto.Protocol}'. Must be one of: {string.Join(", ", Enum.GetNames<ProtocolType>())}", nameof(dto.Protocol));
        }

        return new Device
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Protocol = protocol,
            Host = dto.Host,
            Port = dto.Port,
            Username = dto.Username,
            Password = new EncryptedCredential(dto.Password),
            WakeOnLanEnabled = dto.WakeOnLanEnabled,
            WakeOnLanMacAddress = dto.WakeOnLanMacAddress,
            Schedule = ToSchedule(dto.Schedule),
            RetentionPolicy = ToRetentionPolicy(dto.RetentionPolicy),
            IncludeExcludeRules = ToIncludeExcludeRules(dto.IncludeExcludeRules),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ApplyUpdate(Device device, DeviceUpdateDto dto)
    {
        if (dto.Name != null) device.Name = dto.Name;
        if (dto.Protocol != null)
        {
            if (!Enum.TryParse<ProtocolType>(dto.Protocol, ignoreCase: true, out var protocol))
            {
                throw new ArgumentException($"Invalid protocol '{dto.Protocol}'. Must be one of: {string.Join(", ", Enum.GetNames<ProtocolType>())}", nameof(dto.Protocol));
            }
            device.Protocol = protocol;
        }
        if (dto.Host != null) device.Host = dto.Host;
        if (dto.Port.HasValue) device.Port = dto.Port.Value;
        if (dto.Username != null) device.Username = dto.Username;
        if (dto.Password != null) device.Password = new EncryptedCredential(dto.Password);
        if (dto.WakeOnLanEnabled.HasValue) device.WakeOnLanEnabled = dto.WakeOnLanEnabled.Value;
        if (dto.WakeOnLanMacAddress != null) device.WakeOnLanMacAddress = dto.WakeOnLanMacAddress;
        if (dto.Schedule != null) device.Schedule = ToSchedule(dto.Schedule);
        if (dto.RetentionPolicy != null) device.RetentionPolicy = ToRetentionPolicy(dto.RetentionPolicy);
        if (dto.IncludeExcludeRules != null) device.IncludeExcludeRules = ToIncludeExcludeRules(dto.IncludeExcludeRules);
        
        device.UpdatedAt = DateTime.UtcNow;
    }

    public ShareDto ToShareDto(Share share)
    {
        return new ShareDto
        {
            Id = share.Id,
            DeviceId = share.DeviceId,
            Name = share.Name,
            Path = share.Path,
            Enabled = share.Enabled,
            CreatedAt = share.CreatedAt,
            UpdatedAt = share.UpdatedAt
        };
    }

    public ShareDetailDto ToShareDetailDto(Share share, Backup? lastBackup = null)
    {
        return new ShareDetailDto
        {
            Id = share.Id,
            DeviceId = share.DeviceId,
            Name = share.Name,
            Path = share.Path,
            Enabled = share.Enabled,
            CreatedAt = share.CreatedAt,
            UpdatedAt = share.UpdatedAt,
            Schedule = ToScheduleDto(share.Schedule),
            RetentionPolicy = ToRetentionPolicyDto(share.RetentionPolicy),
            IncludeExcludeRules = ToIncludeExcludeRulesDto(share.IncludeExcludeRules),
            LastBackup = lastBackup != null ? ToBackupDto(lastBackup) : null
        };
    }

    public Share ToShare(Guid deviceId, ShareCreateDto dto)
    {
        return new Share
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = dto.Name,
            Path = dto.Path,
            Enabled = dto.Enabled,
            Schedule = ToSchedule(dto.Schedule),
            RetentionPolicy = ToRetentionPolicy(dto.RetentionPolicy),
            IncludeExcludeRules = ToIncludeExcludeRules(dto.IncludeExcludeRules),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void ApplyUpdate(Share share, ShareUpdateDto dto)
    {
        if (dto.Name != null) share.Name = dto.Name;
        if (dto.Path != null) share.Path = dto.Path;
        if (dto.Enabled.HasValue) share.Enabled = dto.Enabled.Value;
        if (dto.Schedule != null) share.Schedule = ToSchedule(dto.Schedule);
        if (dto.RetentionPolicy != null) share.RetentionPolicy = ToRetentionPolicy(dto.RetentionPolicy);
        if (dto.IncludeExcludeRules != null) share.IncludeExcludeRules = ToIncludeExcludeRules(dto.IncludeExcludeRules);
        
        share.UpdatedAt = DateTime.UtcNow;
    }

    public BackupJobDto ToBackupJobDto(BackupJob job)
    {
        return new BackupJobDto
        {
            Id = job.Id,
            DeviceId = job.DeviceId,
            ShareId = job.ShareId,
            DeviceName = job.DeviceName,
            ShareName = job.ShareName,
            Type = job.Type.ToString(),
            Status = job.Status.ToString(),
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            BackupId = job.BackupId,
            FilesProcessed = job.FilesProcessed,
            BytesTransferred = job.BytesTransferred,
            ErrorMessage = job.ErrorMessage,
            RetryAttempt = job.RetryAttempt,
            NextRetryAt = job.NextRetryAt,
            CommandLine = job.CommandLine
        };
    }

    public BackupDto ToBackupDto(Backup backup)
    {
        return new BackupDto
        {
            Id = backup.Id,
            DeviceId = backup.DeviceId,
            ShareId = backup.ShareId,
            DeviceName = backup.DeviceName,
            ShareName = backup.ShareName,
            Timestamp = backup.Timestamp,
            Status = backup.Status.ToString(),
            SharesPaths = backup.SharesPaths,
            FileStats = new FileStatsDto
            {
                New = backup.FilesNew,
                Changed = backup.FilesChanged,
                Unmodified = backup.FilesUnmodified
            },
            DataStats = new DataStatsDto
            {
                Added = backup.DataAdded,
                Processed = backup.DataProcessed
            },
            Duration = backup.Duration.ToString(),
            ErrorMessage = backup.ErrorMessage,
            CreatedByJobId = backup.CreatedByJobId
        };
    }

    public ScheduleDto? ToScheduleDto(Schedule? schedule)
    {
        if (schedule == null) return null;
        
        return new ScheduleDto
        {
            CronExpression = schedule.CronExpression,
            TimeWindowStart = schedule.TimeWindowStart,
            TimeWindowEnd = schedule.TimeWindowEnd
        };
    }

    public Schedule? ToSchedule(ScheduleDto? dto)
    {
        if (dto == null) return null;
        
        return new Schedule
        {
            CronExpression = dto.CronExpression,
            TimeWindowStart = dto.TimeWindowStart,
            TimeWindowEnd = dto.TimeWindowEnd
        };
    }

    public RetentionPolicyDto? ToRetentionPolicyDto(RetentionPolicy? policy)
    {
        if (policy == null) return null;
        
        return new RetentionPolicyDto
        {
            KeepLatest = policy.KeepLatest,
            KeepDaily = policy.KeepDaily,
            KeepWeekly = policy.KeepWeekly,
            KeepMonthly = policy.KeepMonthly,
            KeepYearly = policy.KeepYearly
        };
    }

    public RetentionPolicy? ToRetentionPolicy(RetentionPolicyDto? dto)
    {
        if (dto == null) return null;
        
        return new RetentionPolicy
        {
            KeepLatest = dto.KeepLatest,
            KeepDaily = dto.KeepDaily,
            KeepWeekly = dto.KeepWeekly,
            KeepMonthly = dto.KeepMonthly,
            KeepYearly = dto.KeepYearly
        };
    }

    public IncludeExcludeRulesDto? ToIncludeExcludeRulesDto(IncludeExcludeRules? rules)
    {
        if (rules == null) return null;
        
        return new IncludeExcludeRulesDto
        {
            ExcludePatterns = rules.ExcludePatterns,
            ExcludeRegex = rules.ExcludeRegex,
            IncludeOnlyRegex = rules.IncludeOnlyRegex,
            ExcludeIfPresent = rules.ExcludeIfPresent
        };
    }

    public IncludeExcludeRules? ToIncludeExcludeRules(IncludeExcludeRulesDto? dto)
    {
        if (dto == null) return null;
        
        return new IncludeExcludeRules
        {
            ExcludePatterns = dto.ExcludePatterns,
            ExcludeRegex = dto.ExcludeRegex,
            IncludeOnlyRegex = dto.IncludeOnlyRegex,
            ExcludeIfPresent = dto.ExcludeIfPresent
        };
    }
}
