using System.ComponentModel.DataAnnotations;

namespace BackupChrono.Api.DTOs;

public class DeviceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool WakeOnLanEnabled { get; set; }
    public string? WakeOnLanMacAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DeviceDetailDto : DeviceDto
{
    public ScheduleDto? Schedule { get; set; }
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
    public List<ShareDto> Shares { get; set; } = new();
    public BackupDto? LastBackup { get; set; }
}

public class DeviceCreateDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Protocol { get; set; } = string.Empty;
    
    [Required]
    public string Host { get; set; } = string.Empty;
    
    public int? Port { get; set; }
    
    [Required]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
    
    public bool WakeOnLanEnabled { get; set; }
    
    public string? WakeOnLanMacAddress { get; set; }
    
    public ScheduleDto? Schedule { get; set; }
    
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
}

public class DeviceUpdateDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    public string? Protocol { get; set; }
    
    public string? Host { get; set; }
    
    public int? Port { get; set; }
    
    public string? Username { get; set; }
    
    public string? Password { get; set; }
    
    public bool? WakeOnLanEnabled { get; set; }
    
    public string? WakeOnLanMacAddress { get; set; }
    
    public ScheduleDto? Schedule { get; set; }
    
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
}

public class ScheduleDto
{
    public string CronExpression { get; set; } = string.Empty;
    public TimeOnly? TimeWindowStart { get; set; }
    public TimeOnly? TimeWindowEnd { get; set; }
}

public class RetentionPolicyDto
{
    public int KeepLatest { get; set; } = 7;
    public int KeepDaily { get; set; } = 7;
    public int KeepWeekly { get; set; } = 4;
    public int KeepMonthly { get; set; } = 12;
    public int KeepYearly { get; set; } = 3;
}

public class IncludeExcludeRulesDto
{
    public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
    public string[] ExcludeRegex { get; set; } = Array.Empty<string>();
    public string[] IncludeOnlyRegex { get; set; } = Array.Empty<string>();
    public string[] ExcludeIfPresent { get; set; } = Array.Empty<string>();
}

public class ShareDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ShareDetailDto : ShareDto
{
    public ScheduleDto? Schedule { get; set; }
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
    public BackupDto? LastBackup { get; set; }
}

public class ShareCreateDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Path { get; set; } = string.Empty;
    
    public bool Enabled { get; set; } = true;
    
    public ScheduleDto? Schedule { get; set; }
    
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
}

public class ShareUpdateDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    public string? Path { get; set; }
    
    public bool? Enabled { get; set; }
    
    public ScheduleDto? Schedule { get; set; }
    
    public RetentionPolicyDto? RetentionPolicy { get; set; }
    
    public IncludeExcludeRulesDto? IncludeExcludeRules { get; set; }
}

public class BackupJobDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? ShareId { get; set; }
    public string? DeviceName { get; set; }
    public string? ShareName { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? BackupId { get; set; }
    public long? FilesProcessed { get; set; }
    public long? BytesTransferred { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryAttempt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? CommandLine { get; set; }
}

public class BackupDto
{
    public string Id { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public Guid? ShareId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? ShareName { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string> SharesPaths { get; set; } = new();
    public FileStatsDto FileStats { get; set; } = new();
    public DataStatsDto DataStats { get; set; } = new();
    public string? Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? CreatedByJobId { get; set; }
}

public class FileStatsDto
{
    public int New { get; set; }
    public int Changed { get; set; }
    public int Unmodified { get; set; }
}

public class DataStatsDto
{
    public long Added { get; set; }
    public long Processed { get; set; }
}

public class BackupDetailDto : BackupDto
{
    public DirectoryStatsDto DirectoryStats { get; set; } = new();
    public SnapshotInfoDto SnapshotInfo { get; set; } = new();
    public DeduplicationInfoDto DeduplicationInfo { get; set; } = new();
    public List<BackupShareDto> Shares { get; set; } = new();
}

public class DirectoryStatsDto
{
    public int New { get; set; }
    public int Changed { get; set; }
    public int Unmodified { get; set; }
}

public class SnapshotInfoDto
{
    public string SnapshotId { get; set; } = string.Empty;
    public string? ParentSnapshot { get; set; }
    public int ExitCode { get; set; }
}

public class DeduplicationInfoDto
{
    public int DataBlobs { get; set; }
    public int TreeBlobs { get; set; }
    public string Ratio { get; set; } = string.Empty;
    public string SpaceSaved { get; set; } = string.Empty;
    public string ContentDedup { get; set; } = string.Empty;
    public string UniqueStorage { get; set; } = string.Empty;
}

public class BackupShareDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long Size { get; set; }
}

public class BackupLogsDto
{
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<ProgressLogEntryDto> ProgressLog { get; set; } = new();
}

public class ProgressLogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public float? PercentDone { get; set; }
    public List<string>? CurrentFiles { get; set; }
    public int? FilesDone { get; set; }
    public long? BytesDone { get; set; }
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Latency { get; set; }
}

public class WakeOnLanResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TriggerBackupRequest
{
    [Required]
    public Guid DeviceId { get; set; }
    
    public Guid? ShareId { get; set; }
}

public class RestoreRequestDto
{
    [Required]
    public string TargetPath { get; set; } = string.Empty;
    
    public List<string>? IncludePaths { get; set; }
    
    public bool RestoreToSource { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
}
