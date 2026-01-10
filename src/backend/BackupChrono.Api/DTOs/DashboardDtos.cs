using System.Text.Json.Serialization;

namespace BackupChrono.Api.DTOs;

public class DashboardSummaryDto
{
    public DashboardStatsDto Stats { get; set; } = new();
    public List<DeviceDashboardDto> Devices { get; set; } = new();
}

public class DashboardStatsDto
{
    public int TotalDevices { get; set; }
    public int TotalShares { get; set; }
    public long TotalStoredBytes { get; set; }
    public int RecentFailures { get; set; } // Last 24h
    public int RunningJobs { get; set; }
    public string SystemHealth { get; set; } = "Healthy";
}

public class DeviceDashboardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "SMB", "Ssh", etc.
    public string Status { get; set; } = "Unknown";
    public List<ShareDashboardDto> Shares { get; set; } = new();
}

public class ShareDashboardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown"; // Success, Failed, Running, Warning, Disabled
    public DateTime? LastBackupTime { get; set; }
    public DateTime? NextBackupTime { get; set; }
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public string? LastBackupId { get; set; } // Snapshot ID
    public Guid? LastJobId { get; set; } // Job ID
}
