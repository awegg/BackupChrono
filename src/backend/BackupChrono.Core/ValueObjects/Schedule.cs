namespace BackupChrono.Core.ValueObjects;

/// <summary>
/// Defines when backup jobs should run using cron expressions and optional time windows.
/// </summary>
public class Schedule
{
    /// <summary>
    /// Cron expression defining the backup schedule (e.g., "0 2 * * *" for 2 AM daily).
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Optional backup window start time (e.g., "02:00"). Backups will only run within this window.
    /// </summary>
    public TimeOnly? TimeWindowStart { get; init; }

    /// <summary>
    /// Optional backup window end time (e.g., "06:00"). Backups will only run within this window.
    /// </summary>
    public TimeOnly? TimeWindowEnd { get; init; }

    /// <summary>
    /// Validates that the schedule configuration is correct.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(CronExpression))
            return false;

        if (TimeWindowStart.HasValue && TimeWindowEnd.HasValue)
        {
            return TimeWindowEnd.Value > TimeWindowStart.Value;
        }

        return true;
    }
}
