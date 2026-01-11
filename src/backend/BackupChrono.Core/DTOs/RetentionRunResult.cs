namespace BackupChrono.Core.DTOs;

/// <summary>
/// Result of a retention policy execution for a device/share scope.
/// </summary>
public class RetentionRunResult
{
    public string DeviceName { get; set; } = string.Empty;
    public string? ShareName { get; set; }
    public int SnapshotsPruned { get; set; }
    public long SpaceReclaimedBytes { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Error { get; set; }
}