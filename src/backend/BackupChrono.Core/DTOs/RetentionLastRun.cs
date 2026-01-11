namespace BackupChrono.Core.DTOs;

/// <summary>
/// Summary of the last retention policy run across all devices.
/// </summary>
public class RetentionLastRun
{
    public DateTime Timestamp { get; set; }
    public int TotalSnapshotsPruned { get; set; }
    public long TotalSpaceReclaimedBytes { get; set; }
}