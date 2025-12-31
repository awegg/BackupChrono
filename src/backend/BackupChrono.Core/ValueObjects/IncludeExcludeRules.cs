namespace BackupChrono.Core.ValueObjects;

/// <summary>
/// Defines which files and directories to include or exclude from backups.
/// Supports glob patterns, regex patterns, and marker files.
/// </summary>
public class IncludeExcludeRules
{
    /// <summary>
    /// Glob patterns to exclude (e.g., "*.tmp", "node_modules/").
    /// </summary>
    public string[] ExcludePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Regex patterns for exclusion.
    /// </summary>
    public string[] ExcludeRegex { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Regex patterns for inclusion-only (BackupFilesOnly mode).
    /// Cannot be used with ExcludeRegex.
    /// </summary>
    public string[] IncludeOnlyRegex { get; init; } = Array.Empty<string>();

    /// <summary>
    /// File markers that indicate a directory should be excluded (e.g., ".nobackup").
    /// If this file exists in a directory, the entire directory is excluded.
    /// </summary>
    public string[] ExcludeIfPresent { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Validates that the rules configuration is correct.
    /// </summary>
    public bool IsValid()
    {
        // Cannot specify both ExcludeRegex and IncludeOnlyRegex (conflicting strategies)
        if (ExcludeRegex.Length > 0 && IncludeOnlyRegex.Length > 0)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the default include/exclude rules.
    /// </summary>
    public static IncludeExcludeRules Default => new()
    {
        ExcludePatterns = new[]
        {
            "*.tmp",
            "*.temp",
            "Thumbs.db",
            ".DS_Store",
            "$RECYCLE.BIN/"
        },
        ExcludeIfPresent = new[] { ".nobackup" }
    };
}
