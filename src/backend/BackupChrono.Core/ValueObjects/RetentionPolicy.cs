namespace BackupChrono.Core.ValueObjects;

/// <summary>
/// Defines how long backups should be kept using various retention strategies.
/// Maps to restic's forget command with --keep-* flags.
/// </summary>
public class RetentionPolicy
{
    /// <summary>
    /// Keep last N backups regardless of age.
    /// </summary>
    public int KeepLatest { get; init; } = 7;

    /// <summary>
    /// Keep one backup per day for N days.
    /// </summary>
    public int KeepDaily { get; init; } = 7;

    /// <summary>
    /// Keep one backup per week for N weeks.
    /// </summary>
    public int KeepWeekly { get; init; } = 4;

    /// <summary>
    /// Keep one backup per month for N months.
    /// </summary>
    public int KeepMonthly { get; init; } = 12;

    /// <summary>
    /// Keep one backup per year for N years.
    /// </summary>
    public int KeepYearly { get; init; } = 3;

    /// <summary>
    /// Validates that the retention policy has at least one keep value greater than zero.
    /// </summary>
    public bool IsValid()
    {
        return KeepLatest >= 0 && KeepDaily >= 0 && KeepWeekly >= 0 && KeepMonthly >= 0 && KeepYearly >= 0
               && (KeepLatest > 0 || KeepDaily > 0 || KeepWeekly > 0 || KeepMonthly > 0 || KeepYearly > 0);
    }
}
