namespace BackupChrono.Api.DTOs;

/// <summary>
/// Complete backup overview dashboard data.
/// </summary>
public class BackupOverviewDto
{
    /// <summary>
    /// Number of devices with warnings or failures.
    /// </summary>
    public int DevicesNeedingAttention { get; set; }

    /// <summary>
    /// Total amount of protected data across all backups in terabytes.
    /// </summary>
    public double TotalProtectedDataTB { get; set; }

    /// <summary>
    /// Number of backup failures in the last 24 hours.
    /// </summary>
    public int RecentFailures { get; set; }

    /// <summary>
    /// List of all devices with their shares and backup status.
    /// </summary>
    public List<DeviceOverviewDto> Devices { get; set; } = new();
}

/// <summary>
/// Device overview with aggregated status and metrics.
/// </summary>
public class DeviceOverviewDto
{
    /// <summary>
    /// Device unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Device name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Overall device status (worst status among all shares).
    /// Values: "Success", "Failed", "Running", "Warning", "Disabled", "Partial"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Total size of all backups for this device in gigabytes.
    /// </summary>
    public double SizeGB { get; set; }

    /// <summary>
    /// Total number of files backed up across all shares.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// List of shares belonging to this device.
    /// </summary>
    public List<ShareOverviewDto> Shares { get; set; } = new();
}

/// <summary>
/// Share overview with backup status and metrics.
/// </summary>
public class ShareOverviewDto
{
    /// <summary>
    /// Share unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Share name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Share path on the device.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last successful backup.
    /// Null if never backed up.
    /// </summary>
    public DateTime? LastBackupTimestamp { get; set; }

    /// <summary>
    /// Current backup status.
    /// Values: "Success", "Failed", "Running", "Warning", "Disabled", "Partial"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Size of the last backup in gigabytes.
    /// </summary>
    public double SizeGB { get; set; }

    /// <summary>
    /// Number of files in the last backup.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Indicates if the backup is stale (older than 2 days or never backed up).
    /// </summary>
    public bool IsStale { get; set; }
}
